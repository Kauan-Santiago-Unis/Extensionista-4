using Aplicativo.Core.Models;
using Aplicativo.Mobile.Services;
using Aplicativo.Mobile.Views;
using static Aplicativo.Mobile.Views.Ui;
namespace Aplicativo.Mobile;

public partial class FleetPage
{
    private void Trips()
    {
        Header("Viagens", () => OpenForm("trip"));
        body.Add(Text("Registros locais: abertura e fechamento funcionam sem internet. A sincronização automática aguarda a API.", 12, Muted));
        if (Can("manageFleet")) body.Add(Button("Gerenciar rotas", () => Go(7), true));
        var records = store.Data.Trips.Where(t => role != "Operador/Motorista" || t.DriverId == Actor).OrderByDescending(t => t.StartedAt).ToList();
        foreach (var t in records)
        {
            var route = store.Data.Routes.FirstOrDefault(r => r.Id == t.RouteId);
            var content = Stack(Row(Text(t.Plate, 16, bold: true), Badge(t.Status)), Text(route?.ToString() ?? "Rota não encontrada", 13), Text($"Motorista: {store.Data.Users.FirstOrDefault(u => u.Id == t.DriverId)?.Name}", 12, Muted), Text($"Início: {t.InitialMileage} km • {t.StartedAt:dd/MM HH:mm}", 12));
            if (t.FinalMileage is null) content.Add(Button("Fechar viagem", async () =>
            {
                var input = await DisplayPromptAsync("Fechar viagem", "Hodômetro final (maior que o inicial)", "Concluir", "Cancelar", keyboard: Keyboard.Numeric);
                if (input is null) return;
                if (!int.TryParse(input, out var final)) { await DisplayAlertAsync("Valor inválido", "Informe um hodômetro inteiro.", "OK"); return; }
                await Mutate("trips", d => { var current = d.Trips.Single(x => x.Id == t.Id); if (role == "Operador/Motorista" && current.DriverId != Actor) throw new ArgumentException("Acesso negado a esta viagem."); DemoStore.FinishTrip(d, t.Id, final); }, "trip-finish");
            }));
            else content.Add(Text($"Final: {t.FinalMileage} km • Rodados: {t.Distance} km", 13, Blue, true));
            body.Add(Card(content));
        }
        if (records.Count == 0) body.Add(Empty("Sem viagens para este perfil. Abra uma viagem com um veículo disponível."));
    }
    private void Routes()
    {
        Header("Rotas", () => OpenForm("route")); body.Add(Button("Voltar às viagens", () => Go(6), true));
        foreach (var r in store.Data.Routes) body.Add(Card(Stack(Text(r.ToString(), 16, bold: true), Text($"Distância estimada: {r.EstimatedKm:N1} km", 13, Muted))));
        if (store.Data.Routes.Count == 0) body.Add(Empty("Cadastre uma origem e um destino."));
    }
    private void Maintenances()
    {
        Header("Manutenções", () => OpenForm("maintenance")); body.Add(Button("Voltar à frota", () => Go(1), true));
        body.Add(Filters(["Todos", "Pendentes", "Concluídas"], maintenanceFilter, value => { maintenanceFilter = value; Render(); }));
        var records = store.Data.Maintenances.Where(m => maintenanceFilter == "Todos" || m.Status == (maintenanceFilter == "Pendentes" ? "Pendente" : "Concluída")).OrderByDescending(m => m.Date).ToList();
        foreach (var m in records)
        {
            var content = Stack(Row(Text(m.Plate, 16, bold: true), Badge(m.Status)), Text(m.Description), Text($"{Date(m.Date)} • {Money(m.Cost)}"), Text($"Pagamento: {m.PaymentStatus} • {m.PaymentMethod}", 12, Muted));
            if (m.Status == "Pendente") content.Add(Button("Concluir serviço", async () => await Mutate("manageFleet", d =>
            {
                var current = d.Maintenances.Single(x => x.Id == m.Id); d.Maintenances[d.Maintenances.IndexOf(current)] = current with { Status = "Concluída" };
                var v = d.Vehicles.Single(x => x.Plate == m.Plate);
                if (!d.Maintenances.Any(x => x.Plate == m.Plate && x.Status == "Pendente")) d.Vehicles[d.Vehicles.IndexOf(v)] = v with { Status = "Disponível" };
            }, "maintenance-complete"), true));
            body.Add(Card(content));
        }
        if (records.Count == 0) body.Add(Empty("Sem manutenções neste filtro."));
    }
    private void Fuel()
    {
        Header("Abastecimentos", () => OpenForm("fuel")); body.Add(Button("Voltar à frota", () => Go(1), true));
        body.Add(Card(Stack(Text("Total pago", 12, Colors.White), Text(Money(store.Data.Refuelings.Sum(x => x.Cost)), 28, Colors.White, true), Text($"{store.Data.Refuelings.Where(x => x.Fuel != "GNV").Sum(x => x.Liters):N1} litros • {store.Data.Refuelings.Where(x => x.Fuel == "GNV").Sum(x => x.Liters):N1} m³ GNV", 12, Colors.White)), Blue));
        foreach (var x in store.Data.Refuelings.OrderByDescending(x => x.Date)) body.Add(Card(Stack(Row(Text(x.Plate, 15, bold: true), Text(Money(x.Cost), 14, Blue)), Text($"{x.Fuel} • {x.Liters:N1} {(x.Fuel == "GNV" ? "m³" : "L")} • {Date(x.Date)}", 12, Muted))));
        if (store.Data.Refuelings.Count == 0) body.Add(Empty("Registre o primeiro abastecimento."));
    }
    private void LogisticsStats()
    {
        body.Add(Text("Eficiência logística", 20, Blue, true));
        var trips = store.Data.Trips.Where(t => t.Distance > 0).ToList();
        decimal Revenue(Trip t) => store.Ledger.Where(x => x.TripId == t.Id).Sum(x => x.Amount);
        decimal Cost(Trip t) => -store.Ledger.Where(x => x.TripId == t.Id && x.Amount < 0).Sum(x => x.Amount);
        body.Add(Text("Top 3 rotas mais lucrativas", 16, bold: true));
        foreach (var r in trips.GroupBy(t => t.RouteId).Select(g => (Id: g.Key, Profit: g.Sum(Revenue))).OrderByDescending(x => x.Profit).Take(3)) body.Add(Card(Row(Text(store.Data.Routes.FirstOrDefault(x => x.Id == r.Id)?.ToString() ?? "Rota"), Text(Money(r.Profit), 16, r.Profit < 0 ? Red : Green))));
        body.Add(Text("Top 3 veículos por maior custo/km", 16, bold: true));
        foreach (var v in trips.GroupBy(t => t.Plate).Select(g => (Plate: g.Key, Cost: g.Sum(Cost) / g.Sum(t => t.Distance))).OrderByDescending(x => x.Cost).Take(3)) body.Add(Card(Row(Text(v.Plate), Text(Money(v.Cost) + "/km", 16, Blue))));
        foreach (var t in trips)
        {
            var liters = store.Data.Refuelings.Where(f => f.TripId == t.Id && f.Fuel != "GNV").Sum(f => f.Liters);
            body.Add(Card(Stack(Text($"{t.Plate} • {t.Distance} km", 15, bold: true), Text($"Resultado: {Money(Revenue(t))} • Custo/km: {Money(Cost(t) / t.Distance)}", 12), Text(liters > 0 ? $"Consumo estimado: {t.Distance / liters:N2} km/L" : "Sem litros vinculados para calcular consumo", 12, Muted))));
        }
        body.Add(Text("Estatísticas sobre viagens concluídas e movimentações vinculadas (pagas ou pendentes). Consumo estimado pelos abastecimentos informados; GNV não entra em km/L.", 11, Muted));
        if (trips.Count == 0) body.Add(Empty("Conclua viagens e vincule receitas e despesas para exibir estatísticas."));
    }
}
