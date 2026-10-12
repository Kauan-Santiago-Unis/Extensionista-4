using System.Globalization;
using Aplicativo.Core.Models;
using Aplicativo.Mobile.Services;
using Aplicativo.Mobile.Views;
using static Aplicativo.Mobile.Views.Ui;

namespace Aplicativo.Mobile;

public partial class FleetPage : ContentPage
{
    private readonly DemoStore store;
    private readonly GoogleAuthService authService;
    private AppSession? session = null;
    private bool signingIn;
    private string DisplayName => role == "Operador/Motorista" ? "Motorista" : store.Data.UserName;
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    private bool loaded;
    private int tab;
    private string vehicleFilter = "Todos", maintenanceFilter = "Todos", search = "";
    private DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private VerticalStackLayout body = new();
    private VerticalStackLayout vehicleList = new();
    private static string Money(decimal value) => value.ToString("C2", Br);
    private static string Date(DateTime value) => value.ToString("dd/MM/yyyy", Br);

    public FleetPage(GoogleAuthService authService, DemoStore store) { InitializeComponent(); this.authService = authService; this.store = store; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        loaded = true;
        try { await store.LoadAsync(); }
        catch (Exception ex) when (ex is IOException or SQLite.SQLiteException)
        {
            loaded = false;
            await DisplayAlertAsync("Banco local", "Não foi possível abrir o banco. Seus dados não foram substituídos. Libere espaço ou tente novamente.", "OK");
            return;
        }
        try { session = await authService.GetStoredSessionAsync(); }
        catch (Exception) { await DisplayAlertAsync("Sessão", "Não foi possível restaurar sua sessão. Entre novamente.", "Entendi"); }
        if (session is null) Login(); else PendingApproval();
        if (store.LoadWarning is not null) await DisplayAlertAsync("Dados locais", store.LoadWarning, "Entendi");
    }

    private async Task SignInAsync()
    {
        if (signingIn) return;
        signingIn = true;
        Root.IsEnabled = false;
        try
        {
            session = await authService.SignInAsync();
            PendingApproval();
        }
        catch (OperationCanceledException)
        { await DisplayAlertAsync("Login cancelado", "Você pode tentar novamente quando quiser.", "Entendi"); }
        catch (HttpRequestException)
        { await DisplayAlertAsync("Falha no login", "Não foi possível conectar ao Google ou à API. Verifique sua conexão e se o perfil FullStack está em execução.", "Entendi"); }
        catch (Exception)
        { await DisplayAlertAsync("Falha no login", "Não foi possível concluir o login. Confira a configuração Google e a execução da API.", "Entendi"); }
        finally { signingIn = false; Root.IsEnabled = true; }
    }

    private async Task SignOutAsync()
    {
        if (signingIn) return;
        signingIn = true;
        Root.IsEnabled = false;
        try
        {
            await authService.SignOutAsync();
            session = null;
            tab = 0;
            Login();
        }
        catch (Exception)
        { await DisplayAlertAsync("Não foi possível sair", "Tente novamente para encerrar a sessão salva.", "Entendi"); }
        finally { signingIn = false; Root.IsEnabled = true; }
    }
    private void Login()
    {
        Root.Clear(); Root.RowDefinitions.Clear();
        var mark = Card(Icon("truck", Blue, 42), Color.FromArgb("#E8EEF7"));
        mark.HorizontalOptions = LayoutOptions.Center;
        var title = Text(store.Data.CompanyName, 32, Blue, true); title.HorizontalTextAlignment = TextAlignment.Center;
        var subtitle = Text("Gestão inteligente de frotas", 14, Muted); subtitle.HorizontalTextAlignment = TextAlignment.Center;
        var illustration = new Image { Source = "fleet_illustration.png", HeightRequest = 190, Aspect = Aspect.AspectFit };
        var chooser = new Picker { Title = "Perfil da demonstração", ItemsSource = AccessPolicy.Roles, SelectedIndex = 0 };
        var googleButton = Button("Entrar com Google", async () => await SignInAsync(), true);
        googleButton.ImageSource = "google_logo.png";
        googleButton.ContentLayout = new Microsoft.Maui.Controls.Button.ButtonContentLayout(Microsoft.Maui.Controls.Button.ButtonContentLayout.ImagePosition.Left, 10);
        var login = Stack(mark, title, subtitle, Card(illustration, Color.FromArgb("#DCEFF8")),
            Text("Sua operação, em um só lugar.", 20, Blue, true),
            Text("Acompanhe veículos, manutenções e resultados com mais clareza.", 14, Muted),
            chooser,
            Button("Entrar na demonstração", () => { if (signingIn) return; session = null; role = chooser.SelectedItem?.ToString() ?? "Operador/Motorista"; tab = role == "Operador/Motorista" ? 6 : 0; Render(); }),
            googleButton,
            Text("AMBIENTE DE DEMONSTRAÇÃO · DADOS LOCAIS", 10, Muted));
        login.Spacing = 20; login.Padding = new Thickness(28, 40);
        login.MaximumWidthRequest = 440; login.HorizontalOptions = LayoutOptions.Fill; login.VerticalOptions = LayoutOptions.Center;
        Root.Add(new ScrollView { Content = login });
    }

    private void Render()
    {
        if (session is not null) { PendingApproval(); return; }
        if (!TabAllowed(tab)) tab = role == "Operador/Motorista" ? 6 : 5;
        Root.Clear(); Root.RowDefinitions.Clear();
        Root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        Root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        body = new VerticalStackLayout { Padding = new Thickness(20, 24, 20, 24), Spacing = 18, MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Fill };
        body.Add(Text($"DEMONSTRAÇÃO LOCAL • {role}", 10, Muted, true));
        switch (tab) { case 0: Home(); break; case 1: Fleet(); break; case 2: Maintenances(); break; case 3: Fuel(); break; case 4: Finance(); break; case 5: Profile(); break; case 6: Trips(); break; case 7: Routes(); break; case 8: Categories(); break; case 9: Payments(); break; case 10: Debts(); break; case 11: Users(); break; }
        Root.Add(new ScrollView { Content = body }, 0, 0);
        Root.Add(NavigationBar(), 0, 1);
    }

    private void Header(string title, Action? add = null)
    {
        var label = Text(title, 24, bold: true); SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level1);
        body.Add(add is null ? label : Row(label, Button("+", add)));
    }

    private View Filters(string[] options, string selected, Action<string> change)
    {
        var row = new HorizontalStackLayout { Spacing = 6 };
        foreach (var option in options) row.Add(Button(option, () => change(option), option != selected));
        return new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = row };
    }

    private void Fleet()
    {
        Header("Frota", Can("manageFleet") ? () => OpenForm("vehicle") : null);
        if (Can("manageFleet")) body.Add(Filters(["Manutenções", "Abastecimentos", "Rotas"], "", value => Go(value == "Manutenções" ? 2 : value == "Abastecimentos" ? 3 : 7)));
        var input = new SearchBar { Placeholder = "Buscar placa ou modelo...", Text = search, FontSize = 14, BackgroundColor = Colors.White, TextColor = Ink, PlaceholderColor = Muted };
        input.TextChanged += (_, e) => { search = e.NewTextValue ?? ""; PopulateVehicles(); };
        body.Add(input);
        body.Add(Filters(["Todos", "Disponível", "Em viagem", "Em manutenção", "Inativo"], vehicleFilter, value => { vehicleFilter = value; Render(); }));
        vehicleList = new VerticalStackLayout { Spacing = 12 }; body.Add(vehicleList); PopulateVehicles();
    }

    private void PopulateVehicles()
    {
        vehicleList.Clear();
        var status = vehicleFilter == "Todos" ? "" : vehicleFilter;
        foreach (var x in store.Data.Vehicles.Where(x => (status == "" || x.Status == status) && ($"{x.Plate} {x.Model}".Contains(search, StringComparison.OrdinalIgnoreCase))))
        {
            var card = Card(Stack(Row(Stack(Text(x.Plate, 15, bold: true), Text($"{x.Brand} {x.Model} {x.Year} • {x.CapacityTons:N2} t", 12, Muted)), Badge(x.Status)),
                new BoxView { HeightRequest = 1, Color = Line }, Row(Text("Quilometragem", 11, Muted), Text($"{x.Mileage.ToString("N0", Br)} km", 12, bold: true))));
            vehicleList.Add(card);
        }
        if (vehicleList.Count == 0) vehicleList.Add(Empty("Tente outra busca ou cadastre um veículo pelo botão +."));
    }


}
