using Aplicativo.Mobile.Views;
using static Aplicativo.Mobile.Views.Ui;
namespace Aplicativo.Mobile;
public partial class FleetPage
{
    private DateTime periodStart = new(DateTime.Today.Year, DateTime.Today.Month, 1), periodEnd = DateTime.Today;
    private int chartMonths = 6;
    private void Home()
    {
        Header($"Olá, {DisplayName.Split(' ')[0]}!");
        if (role == "Gestor de Frota") { LogisticsStats(); return; }
        PeriodFilter();
        var records = store.Ledger.Where(x => x.ReferenceDate.Date >= periodStart && x.ReferenceDate.Date <= periodEnd).ToList();
        var tiles = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 10, RowSpacing = 10 };
        View Metric(string caption, string value, Color color) => Card(Stack(Text(caption, 11, Muted), Text(value, 20, color, true)));
        var balance = store.Ledger.Where(x => x.Status == "Pago/Recebido" && x.ReferenceDate.Date <= periodEnd).Sum(x => x.Amount);
        tiles.Add(Metric("Saldo até o fim do período", Money(balance), balance >= 0 ? Green : Red), 0, 0);
        tiles.Add(Metric("A receber no período", Money(records.Where(x => x.Status == "Pendente" && x.Amount > 0 && x.DueDate.HasValue).Sum(x => x.Amount)), Green), 1, 0);
        tiles.Add(Metric("A pagar no período", Money(-records.Where(x => x.Status == "Pendente" && x.Amount < 0 && x.DueDate.HasValue).Sum(x => x.Amount)), Red), 0, 1);
        tiles.Add(Metric("Viagens ativas no período", store.Data.Trips.Count(t => t.StartedAt.Date <= periodEnd && (t.FinishedAt is null || t.FinishedAt.Value.Date >= periodStart)).ToString(), Blue), 1, 1); body.Add(tiles);
        body.Add(Text("Fluxo de caixa mensal", 18, bold: true));
        body.Add(Filters(["3 meses", "6 meses", "1 ano", "2 anos", "Máximo"], chartMonths switch { 3 => "3 meses", 6 => "6 meses", 12 => "1 ano", 24 => "2 anos", _ => "Máximo" }, value => { chartMonths = value switch { "3 meses" => 3, "6 meses" => 6, "1 ano" => 12, "2 anos" => 24, _ => 0 }; Render(); }));
        var first = store.Ledger.Select(x => x.ReferenceDate).DefaultIfEmpty(periodEnd).Min();
        var count = chartMonths == 0 ? Math.Max(1, (periodEnd.Year - first.Year) * 12 + periodEnd.Month - first.Month + 1) : chartMonths;
        var endMonth = new DateTime(periodEnd.Year, periodEnd.Month, 1);
        var series = Enumerable.Range(0, Math.Min(count, 240)).Select(i => endMonth.AddMonths(i - Math.Min(count, 240) + 1)).Select(m => (Month: m, Income: store.Ledger.Where(x => x.Status == "Pago/Recebido" && x.ReferenceDate >= m && x.ReferenceDate < m.AddMonths(1) && x.Amount > 0).Sum(x => x.Amount), Expense: -store.Ledger.Where(x => x.Status == "Pago/Recebido" && x.ReferenceDate >= m && x.ReferenceDate < m.AddMonths(1) && x.Amount < 0).Sum(x => x.Amount))).ToList();
        var max = Math.Max(1m, series.Select(s => Math.Max(s.Income, s.Expense)).DefaultIfEmpty(1).Max());
        foreach (var s in series) body.Add(Card(Stack(Button(s.Month.ToString("MMM/yyyy", Br), async () => await DisplayAlertAsync("Fluxo de caixa", $"Entradas: {Money(s.Income)}\nSaídas: {Money(s.Expense)}", "OK"), true), new ProgressBar { Progress = (double)(s.Income / max), ProgressColor = Green }, new ProgressBar { Progress = (double)(s.Expense / max), ProgressColor = Red })));
        body.Add(Text("Verde: entradas • Vermelho: saídas. Toque no mês para ver valores. O gráfico termina no mês do filtro; a janela é escolhida acima (até 20 anos).", 11, Muted));
        body.Add(Text("Despesas por categoria (período)", 18, bold: true));
        var slices = records.Where(x => x.Status == "Pago/Recebido" && x.Amount < 0).GroupBy(x => x.Category).Select(g => (g.Key, -g.Sum(x => x.Amount))).ToArray();
        if (slices.Length > 0) body.Add(new ExpenseChart(slices, async (label, amount) => await DisplayAlertAsync(label, Money(amount), "OK"))); else body.Add(Empty("Sem despesas pagas neste período."));
        if (Can("logistics")) LogisticsStats();
    }
    private void PeriodFilter()
    {
        body.Add(Filters(["Este mês", "30 dias", "Ano atual"], "", value => { periodEnd = DateTime.Today; periodStart = value == "Este mês" ? new(DateTime.Today.Year, DateTime.Today.Month, 1) : value == "Ano atual" ? new(DateTime.Today.Year, 1, 1) : DateTime.Today.AddDays(-29); Render(); }));
        var from = new DatePicker { Date = periodStart, Format = "dd/MM/yyyy" }; var to = new DatePicker { Date = periodEnd, Format = "dd/MM/yyyy" };
        body.Add(Card(Stack(Text("Período personalizado", 12, Muted), Row(from, to), Button("Aplicar período", async () => { if (from.Date is null || to.Date is null || from.Date > to.Date) { await DisplayAlertAsync("Período inválido", "A data inicial deve ser anterior ou igual à final.", "OK"); return; } periodStart = from.Date.Value.Date; periodEnd = to.Date.Value.Date; Render(); }, true))));
    }
}
