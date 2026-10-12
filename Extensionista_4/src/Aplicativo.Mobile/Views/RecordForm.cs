using System.Globalization;
using System.Text.RegularExpressions;
using Aplicativo.Core.Models;
using Aplicativo.Mobile.Services;
using static Aplicativo.Mobile.Views.Ui;

namespace Aplicativo.Mobile.Views;

internal sealed class RecordForm : ContentPage
{
    private readonly string kind;
    private readonly DemoStore store;
    private readonly Action refresh;
    private readonly Func<bool> allowed;
    private readonly object? editing;
    private readonly Dictionary<string, Entry> inputs = [];
    private readonly Dictionary<string, Picker> choices = [];
    private readonly HashSet<string> optional = [];
    private readonly DatePicker date = new() { Date = DateTime.Today, MaximumDate = DateTime.Today, Format = "dd/MM/yyyy" };
    private readonly DatePicker due = new() { Date = DateTime.Today, Format = "dd/MM/yyyy" };
    private readonly Switch hasDue = new();
    private readonly Label error = Text("", 13, Red);
    private readonly Button save;
    private readonly VerticalStackLayout fields = new() { Spacing = 16, Padding = 24, MaximumWidthRequest = 620, HorizontalOptions = LayoutOptions.Fill };
    private byte[]? receiptBytes;
    private string? receiptExtension, receiptPath;
    private readonly string actor;
    private List<FinancialCategory> categoryOptions = [];
    private List<Trip> tripOptions = [];
    private List<Vehicle> vehicleOptions = [];
    private List<DemoUser> driverOptions = [];

    public RecordForm(string kind, DemoStore store, Action refresh, Func<bool>? allowed = null, object? editing = null, string actor = "admin")
    {
        this.kind = kind; this.store = store; this.refresh = refresh; this.allowed = allowed ?? (() => true); this.editing = editing; this.actor = actor;
        BackgroundColor = Ui.Background;
        var title = kind switch { "vehicle" => "Novo veículo", "maintenance" => "Nova manutenção", "fuel" => "Novo abastecimento", "transaction" => editing is null ? "Nova movimentação" : "Editar movimentação", "route" => "Nova rota", "trip" => "Abrir viagem", "debt" => "Nova dívida", "category" => "Categoria", "payment" => "Forma de pagamento", "company" => "Dados da empresa", _ => "Meu perfil" };
        fields.Add(Row(Text(title, 22, bold: true), Button("Fechar", async () => await Navigation.PopModalAsync(), true)));
        fields.Add(Text("Demonstração local. Campos opcionais estão identificados.", 12, Muted));
        switch (kind)
        {
            case "vehicle":
                Input("plate", "Placa", "ABC-1D23"); Input("brand", "Marca", "Fiat"); Input("model", "Modelo", "Strada");
                Input("year", "Ano de fabricação", "2024", true); Input("capacity", "Capacidade de carga (toneladas)", "0,7", true); Input("mileage", "Hodômetro", "0", true);
                Choice("status", "Situação inicial", ["Disponível", "Inativo"]); fields.Add(Text("Em viagem e Em manutenção são definidos pelas operações correspondentes.", 12, Muted)); break;
            case "maintenance":
                Vehicles(); Input("description", "Serviço", "Troca de óleo"); Input("cost", "Valor total (R$)", "280,00", true);
                Choice("paymentStatus", "Pagamento", ["Pendente", "Pago/Recebido"]); Payments(); DateField("Data da manutenção"); DueField(); break;
            case "fuel":
                Vehicles(); Choice("fuel", "Combustível", ["Diesel", "Gasolina", "Etanol", "GNV"]);
                Input("liters", "Quantidade (litros; m³ para GNV)", "50", true); Input("cost", "Valor pago (R$)", "325,00", true); DateField("Data do pagamento / abastecimento"); Trips(); break;
            case "transaction":
                Input("description", "Descrição", "Frete - Cliente ABC"); Choice("type", "Tipo", ["Entrada", "Saída"]);
                Choice("category", "Categoria", []); choices["type"].SelectedIndexChanged += (_, _) => ReloadCategories();
                Payments(); Input("cost", "Valor (R$)", "1500,00", true); Choice("paymentStatus", "Status", ["Pendente", "Pago/Recebido"]);
                DateField("Data de pagamento/recebimento (se quitado)"); DueField(); Trips(); Receipt();
                if (editing is Transaction tx)
                {
                    inputs["description"].Text = tx.Description; inputs["cost"].Text = Math.Abs(tx.Amount).ToString(CultureInfo.GetCultureInfo("pt-BR"));
                    choices["type"].SelectedItem = tx.Amount > 0 ? "Entrada" : "Saída";
                    choices["category"].SelectedIndex = categoryOptions.FindIndex(c => c.Id == tx.CategoryId || c.Title == tx.Category);
                    choices["payment"].SelectedItem = tx.PaymentMethod; choices["paymentStatus"].SelectedItem = tx.Status;
                    date.Date = tx.PaidAt ?? tx.Date; hasDue.IsToggled = tx.DueDate.HasValue; due.Date = tx.DueDate ?? DateTime.Today;
                    choices["trip"].SelectedIndex = tx.TripId is Guid id ? tripOptions.FindIndex(t => t.Id == id) + 1 : 0;
                    receiptPath = tx.ReceiptPath;
                }
                break;
            case "route": Input("origin", "Origem (cidade/UF)", "São Paulo/SP"); Input("destination", "Destino (cidade/UF)", "Campinas/SP"); Input("km", "Distância estimada (km)", "99", true); break;
            case "trip":
                Vehicles(true); Choice("route", "Rota", store.Data.Routes.Select(r => r.ToString()).ToArray());
                driverOptions = store.Data.Users.Where(u => u.Approved && u.Role == "Operador/Motorista" && (actor != "driver" || u.Id == actor)).ToList();
                Choice("driver", "Motorista responsável", driverOptions.Select(u => u.Name).ToArray());
                Input("mileage", "Hodômetro inicial", "0", true); break;
            case "debt":
                Input("description", "Descrição", "Financiamento do caminhão"); Input("count", "Quantidade de parcelas (1 a 360)", "12", true); Input("cost", "Valor de cada parcela (R$)", "1000,00", true);
                Input("early", "Quitação antecipada (opcional, R$)", "", true, true);
                categoryOptions = store.Data.Categories.Where(c => c.Active && c.Type == "Saída").ToList();
                Choice("category", "Categoria da dívida", categoryOptions.Select(c => c.Title).ToArray()); Payments();
                fields.Add(Stack(Text("Vencimento da primeira parcela", 13, bold: true), Card(due)));
                var total = Text("Total: R$ 0,00", 18, Blue, true); fields.Add(total);
                void UpdateTotal(object? s, TextChangedEventArgs e) { try { total.Text = $"Total: {(Integer("count", 1, 360) * Amount("cost")).ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))}"; } catch (ArgumentException) { total.Text = "Preencha quantidade e valor para calcular."; } }
                inputs["count"].TextChanged += UpdateTotal; inputs["cost"].TextChanged += UpdateTotal; break;
            case "category":
                Input("title", "Título", "Pedágio"); Choice("type", "Tipo", ["Entrada", "Saída"]);
                if (editing is FinancialCategory cat) { inputs["title"].Text = cat.Title; choices["type"].SelectedItem = cat.Type; }
                break;
            case "payment": Input("title", "Nome", "Dinheiro"); break;
            default: Input("name", kind == "company" ? "Nome da empresa" : "Nome completo", "Nome"); inputs["name"].Text = kind == "company" ? store.Data.CompanyName : store.Data.UserName; break;
        }
        error.IsVisible = false; fields.Add(error);
        save = Button("Salvar", async () => await Save()); fields.Add(save);
        fields.Add(Button("Cancelar", async () => await Navigation.PopModalAsync(), true));
        Content = new ScrollView { Content = fields };
    }
    private void Input(string key, string title, string placeholder, bool number = false, bool isOptional = false)
    {
        var entry = new Entry { Placeholder = placeholder, Keyboard = number ? Keyboard.Numeric : Keyboard.Default, TextColor = Ink, PlaceholderColor = Muted, FontSize = 15, MaxLength = 120 };
        SemanticProperties.SetDescription(entry, title); inputs[key] = entry; if (isOptional) optional.Add(key);
        fields.Add(Stack(Text(title, 13, bold: true), Card(entry, padding: new Thickness(10, 0))));
    }
    private void Choice(string key, string title, string[] options)
    {
        var picker = new Picker { Title = "Selecione uma opção", ItemsSource = options, TextColor = Ink, FontSize = 15 };
        SemanticProperties.SetDescription(picker, title); choices[key] = picker;
        fields.Add(Stack(Text(title, 13, bold: true), Card(picker, padding: new Thickness(10, 0))));
    }
    private void Vehicles(bool availableOnly = false)
    {
        vehicleOptions = store.Data.Vehicles.Where(v => !availableOnly || v.Status == "Disponível").ToList();
        Choice("plate", "Veículo", vehicleOptions.Select(x => x.Plate).ToArray());
    }
    private void Payments() => Choice("payment", "Forma de pagamento", store.Data.PaymentMethods.ToArray());
    private void Trips()
    {
        tripOptions = store.Data.Trips.ToList();
        Choice("trip", "Vincular à viagem (opcional)", new[] { "Sem vínculo" }.Concat(tripOptions.Select(t => $"{t.Plate} • {t.StartedAt:dd/MM HH:mm} • {t.Id.ToString()[..6]}")).ToArray()); choices["trip"].SelectedIndex = 0;
    }
    private void ReloadCategories()
    {
        categoryOptions = store.Data.Categories.Where(c => c.Active && c.Type == Selected("type")).ToList();
        choices["category"].ItemsSource = categoryOptions.Select(c => c.Title).ToArray(); choices["category"].SelectedIndex = -1;
    }
    private void DateField(string label) => fields.Add(Stack(Text(label, 13, bold: true), Card(date)));
    private void DueField()
    {
        fields.Add(Row(Text("Informar vencimento (opcional)", 13), hasDue)); due.IsVisible = false;
        hasDue.Toggled += (_, e) => due.IsVisible = e.Value; fields.Add(due);
    }
    private void Receipt()
    {
        var status = Text(editing is Transaction { ReceiptPath: not null } ? "Comprovante já anexado" : "Sem comprovante (opcional)", 12, Muted);
        async Task Pick(bool camera)
        {
            try
            {
                FileResult? file;
                if (camera)
                {
                    if (!MediaPicker.Default.IsCaptureSupported) throw new ArgumentException("A câmera não está disponível neste dispositivo.");
                    file = await MediaPicker.Default.CapturePhotoAsync();
                }
                else file = await FilePicker.Default.PickAsync(new PickOptions { FileTypes = FilePickerFileType.Images, PickerTitle = "Selecionar comprovante" });
                if (file is null) return;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext is not (".jpg" or ".jpeg" or ".png")) throw new ArgumentException("Use uma imagem JPG ou PNG.");
                using var stream = await file.OpenReadAsync(); using var memory = new MemoryStream();
                var buffer = new byte[8192]; int count;
                while ((count = await stream.ReadAsync(buffer)) > 0) { if (memory.Length + count > 10 * 1024 * 1024) throw new ArgumentException("O comprovante deve ter até 10 MB."); await memory.WriteAsync(buffer.AsMemory(0, count)); }
                receiptBytes = memory.ToArray(); receiptExtension = ext; status.Text = "Comprovante selecionado: " + file.FileName;
            }
            catch (Exception ex) { error.Text = "Não foi possível anexar: " + ex.Message; error.IsVisible = true; }
        }
        fields.Add(Button("Selecionar comprovante", async () => await Pick(false), true)); fields.Add(Button("Fotografar comprovante", async () => await Pick(true), true)); fields.Add(status);
    }
    private string Value(string key) => inputs[key].Text?.Trim() ?? "";
    private string Selected(string key) => choices[key].SelectedItem?.ToString() ?? "";
    private decimal Amount(string key)
    {
        if (!Regex.IsMatch(Value(key), @"^\d+(?:[,.]\d{1,2})?$")) throw new ArgumentException("Use valores positivos sem separador de milhar (ex.: 1250,50).");
        if (!decimal.TryParse(Value(key).Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n) || n <= 0 || n > 999999999) throw new ArgumentException("Informe um valor positivo de até 999.999.999.");
        return n;
    }
    private int Integer(string key, int min, int max)
    { if (!int.TryParse(Value(key), out var n) || n < min || n > max) throw new ArgumentException($"Valor inválido: use um inteiro entre {min} e {max}."); return n; }
    private Guid? TripId => choices.TryGetValue("trip", out var picker) && picker.SelectedIndex > 0 ? tripOptions[picker.SelectedIndex - 1].Id : null;
    private async Task Save()
    {
        if (!save.IsEnabled) return;
        save.IsEnabled = false; save.Text = "Salvando...";
        error.IsVisible = false; string? createdReceipt = null;
        try
        {
            if (!allowed()) throw new ArgumentException("Acesso negado para este perfil.");
            if (inputs.Keys.Any(k => !optional.Contains(k) && string.IsNullOrWhiteSpace(Value(k))) || choices.Keys.Any(k => string.IsNullOrEmpty(Selected(k)))) throw new ArgumentException("Preencha os campos obrigatórios e selecione as opções.");
            var day = date.Date ?? throw new ArgumentException("Informe a data.");
            if (day > DateTime.Today) throw new ArgumentException("A data de realização/pagamento não pode estar no futuro.");
            DateTime? dueDate = hasDue.IsToggled ? due.Date ?? throw new ArgumentException("Informe o vencimento.") : null;
            Action<FleetData> change;
            switch (kind)
            {
                case "vehicle":
                    var plate = Value("plate").ToUpperInvariant().Replace("-", "").Replace(" ", "");
                    if (!Regex.IsMatch(plate, @"^[A-Z]{3}\d[A-Z0-9]\d{2}$")) throw new ArgumentException("Use uma placa válida: ABC-1234 ou ABC-1D23."); plate = plate.Insert(3, "-");
                    var vehicle = new Vehicle(plate, Value("model"), Integer("year", 1900, DateTime.Today.Year + 1), Integer("mileage", 0, int.MaxValue), Selected("status")) { Brand = Value("brand"), CapacityTons = Amount("capacity") };
                    change = d => { if (d.Vehicles.Any(v => v.Plate == plate)) throw new ArgumentException("Esta placa já está cadastrada."); d.Vehicles.Add(vehicle); }; break;
                case "maintenance":
                    var maintenance = new Maintenance(Selected("plate"), Value("description"), day, Amount("cost"), "Pendente") { PaymentStatus = Selected("paymentStatus"), PaymentMethod = Selected("payment"), DueDate = dueDate, PaidAt = Selected("paymentStatus") == "Pago/Recebido" ? day : null };
                    change = d => { var v = d.Vehicles.Single(v => v.Plate == maintenance.Plate); if (v.Status == "Em viagem") throw new ArgumentException("Conclua a viagem antes de abrir uma manutenção."); d.Maintenances.Add(maintenance); d.Vehicles[d.Vehicles.IndexOf(v)] = v with { Status = "Em manutenção" }; }; break;
                case "fuel":
                    var fuel = new Refueling(Selected("plate"), Selected("fuel"), Amount("liters"), Amount("cost"), day) { TripId = TripId };
                    change = d => { if (fuel.TripId is Guid id && d.Trips.Single(t => t.Id == id).Plate != fuel.Plate) throw new ArgumentException("A viagem deve pertencer ao veículo selecionado."); d.Refuelings.Add(fuel); }; break;
                case "transaction":
                    var cat = categoryOptions[choices["category"].SelectedIndex];
                    var transaction = new Transaction(Value("description"), cat.Title, Amount("cost") * (Selected("type") == "Saída" ? -1 : 1), editing is Transaction old ? old.Date : DateTime.Today)
                    { Id = editing is Transaction existing ? existing.Id : Guid.NewGuid(), CategoryId = cat.Id, PaymentMethod = Selected("payment"), Status = Selected("paymentStatus"), DueDate = dueDate, PaidAt = Selected("paymentStatus") == "Pago/Recebido" ? day : null, TripId = TripId, CreatedAt = editing is Transaction original ? original.CreatedAt : DateTime.UtcNow, UpdatedAt = editing is null ? null : DateTime.UtcNow, DebtId = (editing as Transaction)?.DebtId, ReceiptPath = receiptPath };
                    if (receiptBytes is not null)
                    {
                        var folder = Path.Combine(FileSystem.AppDataDirectory, "receipts"); Directory.CreateDirectory(folder);
                        createdReceipt = Path.Combine(folder, Guid.NewGuid() + receiptExtension); await File.WriteAllBytesAsync(createdReceipt, receiptBytes);
                        transaction = transaction with { ReceiptPath = createdReceipt };
                    }
                    change = d => { if (editing is Transaction tx) { var index = d.Transactions.FindIndex(x => x.Id == tx.Id); if (index < 0) throw new ArgumentException("Registro não encontrado."); d.Transactions[index] = transaction; } else d.Transactions.Add(transaction); }; break;
                case "route":
                    if (Value("origin").Equals(Value("destination"), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Origem e destino devem ser diferentes.");
                    var route = new RoutePlan(Guid.NewGuid(), Value("origin"), Value("destination"), Amount("km")); change = d => d.Routes.Add(route); break;
                case "trip":
                    var trip = new Trip(Guid.NewGuid(), Selected("plate"), store.Data.Routes[choices["route"].SelectedIndex].Id, driverOptions[choices["driver"].SelectedIndex].Id, Integer("mileage", 0, int.MaxValue), DateTime.Now);
                    change = d => DemoStore.AddTrip(d, trip); break;
                case "debt":
                    var debt = new Debt(Guid.NewGuid(), Value("description"), Integer("count", 1, 360), Amount("cost"), due.Date ?? throw new ArgumentException("Informe o primeiro vencimento."), categoryOptions[choices["category"].SelectedIndex].Id, string.IsNullOrWhiteSpace(Value("early")) ? null : Amount("early"));
                    change = d => DemoStore.AddDebt(d, debt, Selected("payment")); break;
                case "category":
                    var category = new FinancialCategory(editing is FinancialCategory prev ? prev.Id : Guid.NewGuid(), Value("title"), Selected("type"), editing is FinancialCategory previous ? previous.Active : true);
                    change = d => { if (d.Categories.Any(c => c.Id != category.Id && c.Title.Equals(category.Title, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Categoria já cadastrada."); if (editing is FinancialCategory oldCategory) { if (oldCategory.Type != category.Type && d.Transactions.Any(t => t.CategoryId == category.Id)) throw new ArgumentException("Não altere o tipo de uma categoria já utilizada."); d.Categories[d.Categories.FindIndex(c => c.Id == category.Id)] = category; } else d.Categories.Add(category); }; break;
                case "payment": change = d => { if (d.PaymentMethods.Contains(Value("title"), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Forma já cadastrada."); d.PaymentMethods.Add(Value("title")); }; break;
                case "company": change = d => d.CompanyName = Value("name"); break;
                default: change = d => d.UserName = Value("name"); break;
            }
            save.IsEnabled = false; save.Text = "Salvando...";
            await store.CommitAsync(change, kind);
            createdReceipt = null;
            refresh(); await DisplayAlertAsync("Salvo", "Registro salvo neste dispositivo. A API de sincronização ainda não está disponível.", "OK"); await Navigation.PopModalAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        { error.Text = ex is ArgumentException ? ex.Message : "Não foi possível salvar. Verifique os dados e o armazenamento e tente novamente."; error.IsVisible = true; }
        finally { save.IsEnabled = true; save.Text = "Salvar"; if (createdReceipt is not null) { try { File.Delete(createdReceipt); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
    }
}
