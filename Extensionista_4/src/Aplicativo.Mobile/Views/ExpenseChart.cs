using Microsoft.Maui.Graphics;
using static Aplicativo.Mobile.Views.Ui;
namespace Aplicativo.Mobile.Views;
internal sealed class ExpenseChart : VerticalStackLayout
{
    private static readonly Color[] Palette = [Blue, Green, Color.FromArgb("#DC9140"), Red, Color.FromArgb("#7964AB"), Color.FromArgb("#4A9FAF")];
    public ExpenseChart((string Label, decimal Value)[] values, Action<string, decimal> select)
    {
        var total = values.Sum(x => x.Value);
        var graph = new GraphicsView { HeightRequest = 200, WidthRequest = 200, HorizontalOptions = LayoutOptions.Center, Drawable = new Donut(values.Select(x => (float)(x.Value / total)).ToArray()) };
        graph.StartInteraction += (_, e) =>
        {
            var point = e.Touches[0]; var dx = point.X - 100; var dy = point.Y - 100; var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 48 || distance > 90) return;
            var fraction = (Math.Atan2(dy, dx) / (2 * Math.PI) + 1) % 1; double cumulative = 0;
            for (var i = 0; i < values.Length; i++) { cumulative += (double)(values[i].Value / total); if (fraction <= cumulative) { select(values[i].Label, values[i].Value); break; } }
        };
        Add(graph);
        foreach (var value in values) Add(Button($"{value.Label} • {value.Value / total:P1}", () => select(value.Label, value.Value), true));
    }
    private sealed class Donut(float[] parts) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF bounds)
        {
            double start = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                var end = start + parts[i] * Math.PI * 2; var path = new PathF(); path.MoveTo(100, 100);
                for (var a = start; a < end; a += 0.02) path.LineTo(100 + 90 * (float)Math.Cos(a), 100 + 90 * (float)Math.Sin(a));
                path.LineTo(100 + 90 * (float)Math.Cos(end), 100 + 90 * (float)Math.Sin(end)); path.Close(); canvas.FillColor = Palette[i % Palette.Length]; canvas.FillPath(path); start = end;
            }
            canvas.FillColor = Colors.White; canvas.FillCircle(100, 100, 48);
        }
    }
}
