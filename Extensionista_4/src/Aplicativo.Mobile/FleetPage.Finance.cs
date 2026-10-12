using System.Text;
using Aplicativo.Core.Models;
using Aplicativo.Mobile.Services;
using static Aplicativo.Mobile.Views.Ui;
namespace Aplicativo.Mobile;

public partial class FleetPage
{
    private void Finance()
    {
        Header("Financeiro", () => OpenForm("transaction"));
        body.Add(Filters(["Categorias", "Pagamentos", "Dívidas"], "", value => Go(value == "Categorias" ? 8 : value == "Pagamentos" ? 9 : 10)));
        body.Add(Row(Button("‹ Mês anterior", () => { month = month.AddMonths(-1); Render(); }, true), Button("Próximo mês ›", () => { month = month.AddMonths(1); Render(); }, true)));
        body.Add(Text(month.ToString("MMMM yyyy", Br), 17, Blue, true));
        var records = store.Ledger.Where(x => x.ReferenceDate >= month && x.ReferenceDate < month.AddMonths(1)).OrderByDescending(x => x.ReferenceDate).ToList();
        var paid = records.Where(x => x.Status == "Pago/Recebido").ToList();
        body.Add(Card(Stack(Text("Saldo atual (todo o histórico pago)", 12, Muted), Text(Money(store.Balance), 28, store.Balance < 0 ? Red : Blue, true), Text($"Resultado pago do mês: {Money(paid.Sum(x => x.Amount))}", 13))));
        body.Add(Card(Stack(Text($"Entradas do mês: {Money(paid.Where(x => x.Amount > 0).Sum(x => x.Amount))}", 14, Green), Text($"Saídas do mês: {Money(-paid.Where(x => x.Amount < 0).Sum(x => x.Amount))}", 14, Red))));
        body.Add(Text("Previsão dos próximos 3 meses", 17, bold: true));
        for (var i = 0; i < 3; i++) { var end = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(i + 1).AddDays(-1); var amount = store.Forecast(end); body.Add(Card(Row(Text(end.ToString("MMM/yyyy", Br)), Text(Money(amount), 16, amount < 0 ? Red : Green, true)))); }
        var undated = store.Ledger.Count(x => x.Status == "Pendente" && x.DueDate is null);
        if (undated > 0) body.Add(Text($"{undated} pendência(s) sem vencimento não entram na previsão.", 12, Red));
        body.Add(Button("Exportar CSV do mês", async () => await Export(records), true));
        foreach (var x in records)
        {
            var content = Stack(Row(Text(x.Description, 14, bold: true), Badge(x.Status)), Text($"{x.Category} • {x.PaymentMethod}", 12, Muted), Row(Text(Date(x.ReferenceDate), 12), Text(Money(x.Amount), 15, x.Amount > 0 ? Green : Red, true)));
            if (x.Source == "Manual") content.Add(Button("Editar movimentação", () => OpenForm("transaction", x), true));
            if (x.Status == "Pendente") content.Add(Button("Registrar pagamento/recebimento hoje", async () => await Mutate("finance", d =>
            {
                if (x.Source == "Manutenção") { var m = d.Maintenances.Single(m => m.Id == x.Id); d.Maintenances[d.Maintenances.IndexOf(m)] = m with { PaymentStatus = "Pago/Recebido", PaidAt = DateTime.Today }; }
                else { var tx = d.Transactions.Single(t => t.Id == x.Id); d.Transactions[d.Transactions.IndexOf(tx)] = tx with { Status = "Pago/Recebido", PaidAt = DateTime.Today, UpdatedAt = DateTime.UtcNow }; }
            }, "payment"), true));
            if (x.ReceiptPath is not null) content.Add(Button("Ver comprovante", async () => { try { await Launcher.Default.OpenAsync(new OpenFileRequest("Comprovante", new ReadOnlyFile(x.ReceiptPath))); } catch (Exception) { await DisplayAlertAsync("Comprovante", "Não foi possível abrir a imagem local.", "OK"); } }, true));
            body.Add(Card(content));
        }
        if (records.Count == 0) body.Add(Empty("Sem movimentações neste mês."));
    }
    private async Task Export(List<Transaction> rows)
    {
        if (!Can("finance")) return;
        try { var path = Path.Combine(FileSystem.CacheDirectory, $"movimentacoes-{month:yyyy-MM}.csv"); await File.WriteAllTextAsync(path, DemoStore.Csv(rows), new UTF8Encoding(true)); await Share.Default.RequestAsync(new ShareFileRequest { Title = "Relatório financeiro", File = new ShareFile(path, "text/csv") }); }
        catch (Exception) { await DisplayAlertAsync("Exportação", "Não foi possível gerar ou compartilhar o arquivo.", "OK"); }
    }
    private void Categories()
    {
        Header("Categorias", () => OpenForm("category")); body.Add(Button("Voltar ao financeiro", () => Go(4), true));
        foreach (var c in store.Data.Categories)
            body.Add(Card(Stack(Row(Text(c.Title, 16, bold: true), Badge(c.Active ? "Ativo" : "Inativo")), Text(c.Type, 12, Muted), Button("Editar", () => OpenForm("category", c), true), Button(c.Active ? "Desativar" : "Reativar", async () => await Mutate("finance", d => { var index = d.Categories.FindIndex(x => x.Id == c.Id); d.Categories[index] = d.Categories[index] with { Active = !c.Active }; }, "category-status"), true))));
    }
    private void Payments()
    {
        Header("Formas de pagamento", () => OpenForm("payment")); body.Add(Button("Voltar ao financeiro", () => Go(4), true)); foreach (var p in store.Data.PaymentMethods) body.Add(Card(Text(p, 15)));
    }
    private void Debts()
    {
        Header("Dívidas e parcelas", () => OpenForm("debt")); body.Add(Button("Voltar ao financeiro", () => Go(4), true));
        foreach (var d in store.Data.Debts) body.Add(Card(Stack(Text(d.Description, 16, bold: true), Text($"{d.Installments} parcelas de {Money(d.InstallmentAmount)}"), Text($"Total: {Money(d.Installments * d.InstallmentAmount)}", 17, Blue, true), Text($"Primeiro vencimento: {Date(d.FirstDueDate)}", 12, Muted), Text(d.EarlySettlement is decimal value ? $"Quitação informada: {Money(value)}" : "Sem valor de quitação antecipada", 12), Text($"{store.Data.Transactions.Count(t => t.DebtId == d.Id && t.Status == "Pendente")} parcela(s) pendente(s)", 12))));
        if (store.Data.Debts.Count == 0) body.Add(Empty("Cadastre uma dívida para gerar as parcelas locais."));
    }
}
