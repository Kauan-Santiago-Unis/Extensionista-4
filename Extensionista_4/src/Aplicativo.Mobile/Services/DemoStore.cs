using Aplicativo.Mobile.Data.Local;
using System.Text;
using System.Text.Json;
using Aplicativo.Core.Models;

namespace Aplicativo.Mobile.Services;

public sealed class DemoStore
{
    private readonly string path = Path.Combine(FileSystem.AppDataDirectory, "fleet-demo.json");
    private readonly LocalDatabase database;
    public DemoStore() : this(new LocalDatabase(Path.Combine(FileSystem.AppDataDirectory, "logtrack.db3"))) { }
    public DemoStore(LocalDatabase database) { this.database = database; }
    private readonly SemaphoreSlim gate = new(1, 1);
    public FleetData Data { get; private set; } = new();
    public string? LoadWarning { get; private set; }
    private bool recoveryCopied;

    public async Task LoadAsync()
    {
        var saved = await database.ReadAsync();
        if (saved is not null)
        {
            try { Data = JsonSerializer.Deserialize<FleetData>(saved) ?? throw new JsonException(); }
            catch (JsonException ex) { throw new IOException("Banco local inválido; conteúdo preservado.", ex); }
            return;
        }
        if (File.Exists(path))
        {
            try
            {
                Data = JsonSerializer.Deserialize<FleetData>(await File.ReadAllTextAsync(path)) ?? throw new JsonException();
                if (Data.Vehicles is null || Data.Maintenances is null || Data.Refuelings is null || Data.Transactions is null || Data.Categories is null || Data.PaymentMethods is null || Data.Routes is null || Data.Trips is null || Data.Debts is null || Data.Users is null || Data.LocalOperations is null) throw new JsonException();
                // Legacy records retain their previous cash meaning; new maintenance separates payment from service status.
                if (Data.SchemaVersion < 2)
                {
                    Data.Vehicles = Data.Vehicles.Select(v => v with { Status = v.Status == "Ativo" ? "Disponível" : v.Status }).ToList();
                    Data.Maintenances = Data.Maintenances.Select(m => m with { PaymentStatus = m.Status == "Concluída" ? "Pago/Recebido" : "Pendente", PaidAt = m.Status == "Concluída" ? m.Date : null }).ToList();
                    Data.SchemaVersion = 2;
                }
                // Keep the source JSON intact. SQLite is authoritative after successful import.

            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            { LoadWarning = "Não foi possível ler os dados locais. A demonstração foi carregada; o arquivo original será preservado como cópia antes de salvar."; }
        }
        if (File.Exists(path) && LoadWarning is null)
        { await SaveAsync(); return; }
        var day = DateTime.Today;
        Data = new FleetData
        {
            SchemaVersion = 2,
            Vehicles = [new("ABC-1D23", "Strada", 2022, 45230, "Disponível") { Brand = "Fiat", CapacityTons = 0.7m }, new("DEF-4G56", "Delivery", 2021, 78150, "Em manutenção") { Brand = "Volkswagen", CapacityTons = 4 }, new("GHI-7J89", "Master", 2023, 12000, "Disponível") { Brand = "Renault", CapacityTons = 1.5m }, new("JKL-0M12", "Hilux", 2020, 102500, "Inativo") { Brand = "Toyota", CapacityTons = 1 }],
            Maintenances = [new("DEF-4G56", "Troca de óleo", day.AddDays(-2), 280, "Pendente") { DueDate = day.AddDays(10), PaymentMethod = "Boleto" }, new("ABC-1D23", "Alinhamento", day.AddDays(-4), 150, "Concluída") { PaymentStatus = "Pago/Recebido", PaidAt = day.AddDays(-4), PaymentMethod = "Pix" }],
            Refuelings = [new("ABC-1D23", "Diesel", 120, 780, day.AddDays(-1)), new("DEF-4G56", "Diesel", 95, 617.50m, day.AddDays(-3))],
            Transactions = [new("Frete - Cliente ABC", "Frete", 3500, day.AddDays(-1)) { PaymentMethod = "Pix", PaidAt = day.AddDays(-1) }, new("Frete a receber", "Frete", 2200, day) { Status = "Pendente", DueDate = day.AddDays(7), PaymentMethod = "Boleto" }],
            Routes = [new(Guid.NewGuid(), "São Paulo/SP", "Campinas/SP", 99)]
        };
        await SaveAsync();
    }
    public async Task SaveAsync()
    {
        if (LoadWarning is not null && !recoveryCopied && File.Exists(path))
        { File.Copy(path, path + ".recovery-" + DateTime.UtcNow.Ticks, false); recoveryCopied = true; }
        await database.SaveAsync(JsonSerializer.Serialize(Data));
    }

    public async Task CommitAsync(Action<FleetData> mutation, string kind)
    {
        await gate.WaitAsync();
        var snapshot = JsonSerializer.Serialize(Data);
        try
        {
            mutation(Data);
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            Data.LocalOperations.Add(new(id, kind, now));
            var payload = JsonSerializer.Serialize(Data);
            var syncData = JsonSerializer.Deserialize<FleetData>(payload)!;
            syncData.LocalOperations.Clear();
            syncData.Transactions = syncData.Transactions.Select(t => t with { ReceiptPath = null }).ToList();
            await database.SaveAsync(payload, new PendingSyncOperation
            {
                Id = id.ToString(), Scope = "demo", Kind = kind,
                Payload = JsonSerializer.Serialize(syncData), CreatedAtUtc = now
            });
        }
        catch { Data = JsonSerializer.Deserialize<FleetData>(snapshot)!; throw; }
        finally { gate.Release(); }
    }
    public IEnumerable<Transaction> Ledger => Data.Transactions
        .Concat(Data.Refuelings.Select(x => new Transaction($"Abastecimento - {x.Plate}", "Combustível", -x.Cost, x.Date) { Id = x.Id, PaidAt = x.Date, TripId = x.TripId, Source = "Abastecimento" }))
        .Concat(Data.Maintenances.Select(x => new Transaction($"Manutenção - {x.Plate}", "Manutenção", -x.Cost, x.Date) { Id = x.Id, Status = x.PaymentStatus, PaidAt = x.PaidAt, DueDate = x.DueDate, PaymentMethod = x.PaymentMethod, Source = "Manutenção" }));
    public decimal Balance => Ledger.Where(x => x.Status == "Pago/Recebido").Sum(x => x.Amount);
    public decimal Forecast(DateTime end) => Balance + Ledger.Where(x => x.Status == "Pendente" && x.DueDate is DateTime due && due.Date <= end.Date).Sum(x => x.Amount);
    public static void AddTrip(FleetData data, Trip trip)
    {
        var vehicle = data.Vehicles.Single(x => x.Plate == trip.Plate);
        if (vehicle.Status != "Disponível" || data.Trips.Any(t => t.Plate == trip.Plate && t.FinalMileage is null)) throw new ArgumentException("O veículo não está disponível.");
        if (trip.InitialMileage < vehicle.Mileage) throw new ArgumentException("O hodômetro inicial não pode ser menor que o atual.");
        if (!data.Routes.Any(r => r.Id == trip.RouteId) || !data.Users.Any(u => u.Id == trip.DriverId && u.Approved && u.Role == "Operador/Motorista")) throw new ArgumentException("Selecione rota e motorista válidos.");
        data.Trips.Add(trip);
        data.Vehicles[data.Vehicles.IndexOf(vehicle)] = vehicle with { Status = "Em viagem", Mileage = trip.InitialMileage };
    }
    public static void FinishTrip(FleetData data, Guid id, int final)
    {
        var trip = data.Trips.Single(t => t.Id == id);
        if (trip.FinalMileage is not null) throw new ArgumentException("Esta viagem já foi concluída.");
        _ = Aplicativo.Core.Validation.TripValidation.Distance(trip.InitialMileage, final);
        data.Trips[data.Trips.IndexOf(trip)] = trip with { FinalMileage = final, FinishedAt = DateTime.Now };
        var vehicle = data.Vehicles.Single(v => v.Plate == trip.Plate);
        data.Vehicles[data.Vehicles.IndexOf(vehicle)] = vehicle with { Mileage = final, Status = data.Maintenances.Any(m => m.Plate == trip.Plate && m.Status == "Pendente") ? "Em manutenção" : "Disponível" };
    }
    public static void AddDebt(FleetData data, Debt debt, string payment)
    {
        if (debt.Installments is < 1 or > 360 || debt.InstallmentAmount <= 0) throw new ArgumentException("Informe de 1 a 360 parcelas e valor positivo.");
        var category = data.Categories.Single(c => c.Id == debt.CategoryId && c.Active && c.Type == "Saída");
        data.Debts.Add(debt);
        for (var i = 0; i < debt.Installments; i++)
            data.Transactions.Add(new(debt.Description + $" ({i + 1}/{debt.Installments})", category.Title, -debt.InstallmentAmount, DateTime.Today) { Status = "Pendente", DueDate = debt.FirstDueDate.AddMonths(i), DebtId = debt.Id, CategoryId = category.Id, PaymentMethod = payment });
    }
    public static string Csv(IEnumerable<Transaction> rows)
    {
        static string Quote(string? value)
        {
            value ??= "";
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var lines = new List<string> { "Data;Descrição;Categoria;Valor;Status" };
        lines.AddRange(rows.Select(x => string.Join(';', Quote(x.ReferenceDate.ToString("dd/MM/yyyy")), Quote(x.Description), Quote(x.Category), x.Amount.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), Quote(x.Status))));
        return string.Join(Environment.NewLine, lines);
    }
}
