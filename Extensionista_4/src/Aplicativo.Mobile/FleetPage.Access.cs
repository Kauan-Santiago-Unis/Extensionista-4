using Aplicativo.Core.Models;
using Aplicativo.Mobile.Services;
using Aplicativo.Mobile.Views;
using static Aplicativo.Mobile.Views.Ui;

namespace Aplicativo.Mobile;

public partial class FleetPage
{
    private string role = "Administrador";
    private bool busy;
    private string Actor => role == "Operador/Motorista" ? "driver" : "admin";
    private bool Can(string action) => session is null && AccessPolicy.Can(role, action);
    private bool TabAllowed(int index) => Can(index switch { 0 => role == "Gestor de Frota" ? "logistics" : "finance", 1 => "fleet", 2 or 3 or 7 => "manageFleet", 4 or 8 or 9 or 10 => "finance", 6 => "trips", 11 => "admin", _ => "profile" });
    private bool FormAllowed(string kind) => Can(kind switch { "transaction" or "category" or "payment" or "debt" => "finance", "vehicle" or "maintenance" or "fuel" or "route" => "manageFleet", "trip" => "trips", "company" => "admin", _ => "profile" });
    private void PendingApproval()
    {
        Root.Clear(); Root.RowDefinitions.Clear();
        var panel = Stack(Text("Aguardando aprovação", 25, Blue, true), Text(session?.Name ?? "Conta Google", 18), Text("Sua identidade foi autenticada. A API atual ainda não informa aprovação e perfil de acesso. Os módulos de gestão permanecem bloqueados até essa integração.", 15, Muted), Button("Sair", async () => await SignOutAsync()));
        panel.Padding = 28; panel.MaximumWidthRequest = 500; Root.Add(new ScrollView { Content = panel });
    }
    private void Go(int index) { if (!TabAllowed(index)) { _ = DisplayAlertAsync("Acesso negado", "Este perfil não pode acessar este módulo.", "OK"); return; } tab = index; Render(); }
    private View NavigationBar()
    {
        var items = new[] { (0, "Início"), (1, "Frota"), (6, "Viagens"), (4, "Finanças"), (5, "Perfil") }.Where(x => TabAllowed(x.Item1)).ToArray();
        var grid = new Grid { Padding = 6, ColumnSpacing = 4, BackgroundColor = Colors.White, MaximumWidthRequest = 760 };
        for (var i = 0; i < items.Length; i++) { grid.ColumnDefinitions.Add(new(GridLength.Star)); var item = items[i]; var button = Button(item.Item2, () => Go(item.Item1), tab != item.Item1); button.FontSize = 11; button.Padding = new Thickness(2, 8); grid.Add(button, i); }
        return Card(grid, padding: new Thickness(0));
    }
    private void Profile()
    {
        Header("Perfil"); body.Add(Card(Stack(Text(DisplayName, 19, bold: true), Text(role, 14, Blue))));
        if (Can("admin")) { body.Add(Button("Dados da empresa", () => OpenForm("company"), true)); body.Add(Button("Usuários da demonstração", () => Go(11), true)); }
        if (role != "Operador/Motorista") body.Add(Button("Nome da demonstração", () => OpenForm("profile"), true));
        body.Add(Card(Stack(Text("Armazenamento local", 16, bold: true), Text($"{store.Data.LocalOperations.Count} operação(ões) registrada(s) neste dispositivo. Nenhum registro foi enviado ao servidor. Sincronização automática e aprovação real dependem da API.", 12, Muted))));
        body.Add(Button("Sair", async () => await SignOutAsync(), true));
    }
    private void Users()
    {
        Header("Usuários da demonstração"); body.Add(Text("Simulação de gestão de perfis. Não altera contas Google nem permissões no servidor.", 12, Muted));
        foreach (var user in store.Data.Users)
        {
            var picker = new Picker { ItemsSource = AccessPolicy.Roles, SelectedItem = user.Role };
            body.Add(Card(Stack(Text(user.Name, 16, bold: true), Badge(user.Approved ? "Ativo" : "Aguardando aprovação"), picker, Button("Salvar perfil e aprovar", async () => await Mutate("admin", d => { if (user.Id == "admin" && picker.SelectedItem?.ToString() != "Administrador") throw new ArgumentException("Mantenha o administrador da demonstração."); if (user.Id == "driver" && picker.SelectedItem?.ToString() != "Operador/Motorista") throw new ArgumentException("Mantenha o motorista usado na demonstração."); var index = d.Users.FindIndex(u => u.Id == user.Id); d.Users[index] = user with { Role = picker.SelectedItem?.ToString() ?? "Operador/Motorista", Approved = true }; }, "user-role"), true))));
        }
    }
    private async Task Mutate(string permission, Action<FleetData> change, string kind)
    {
        if (busy) return; busy = true;
        try { if (!Can(permission)) throw new ArgumentException("Acesso negado."); await store.CommitAsync(change, kind); Render(); await DisplayAlertAsync("Salvo", "Alteração salva localmente.", "OK"); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException) { await DisplayAlertAsync("Não foi possível concluir", ex is ArgumentException ? ex.Message : "Verifique os dados e o armazenamento.", "OK"); }
        finally { busy = false; }
    }
    private async void OpenForm(string kind, object? editing = null)
    {
        if (!FormAllowed(kind)) { await DisplayAlertAsync("Acesso negado", "Este perfil não pode executar esta ação.", "OK"); return; }
        if (kind is "maintenance" or "fuel" or "trip" && store.Data.Vehicles.Count == 0) { await DisplayAlertAsync("Cadastre um veículo", "Adicione um veículo antes de continuar.", "OK"); return; }
        if (kind == "trip" && (!store.Data.Vehicles.Any(v => v.Status == "Disponível") || store.Data.Routes.Count == 0)) { await DisplayAlertAsync("Viagem indisponível", "É necessário ter uma rota e um veículo disponível.", "OK"); return; }
        await Navigation.PushModalAsync(new RecordForm(kind, store, Render, () => FormAllowed(kind), editing, Actor));
    }
}
