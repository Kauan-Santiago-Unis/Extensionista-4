using Microsoft.Maui.Controls.Shapes;

namespace Aplicativo.Mobile.Views;

internal static class Ui
{
    public static readonly Color Blue = Color.FromArgb("#24538E");
    public static readonly Color Ink = Color.FromArgb("#202936");
    public static readonly Color Muted = Color.FromArgb("#7A8799");
    public static readonly Color Background = Color.FromArgb("#F5F7FA");
    public static readonly Color Line = Color.FromArgb("#E7ECF2");
    public static readonly Color Green = Color.FromArgb("#239660");
    public static readonly Color Red = Color.FromArgb("#D65060");

    public static Label Text(string text, double size = 14, Color? color = null, bool bold = false) => new()
    {
        Text = text, FontSize = size, TextColor = color ?? Ink,
        FontFamily = bold ? "OpenSansSemibold" : "OpenSansRegular",
        VerticalOptions = LayoutOptions.Center
    };

    public static VerticalStackLayout Stack(params View[] views)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        foreach (var view in views) stack.Add(view);
        return stack;
    }

    public static Border Card(View content, Color? color = null, Thickness? padding = null) => new()
    {
        Content = content, BackgroundColor = color ?? Colors.White,
        Stroke = new SolidColorBrush(Line), StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = padding ?? new Thickness(16)
    };

    public static Button Button(string text, Action action, bool secondary = false)
    {
        var button = new Button
        {
            Text = text, FontSize = 13, FontFamily = "OpenSansSemibold", CornerRadius = 9,
            MinimumHeightRequest = 48, Padding = new Thickness(12, 8),
            BackgroundColor = secondary ? Colors.White : Blue, TextColor = secondary ? Blue : Colors.White,
            BorderColor = secondary ? Line : Blue, BorderWidth = 1
        };
        button.Clicked += (_, _) => action();
        return button;
    }

    public static Grid Row(View left, View right)
    {
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 10 };
        grid.Add(left, 0); grid.Add(right, 1);
        return grid;
    }

    public static Border Badge(string status)
    {
        var color = status is "Ativo" or "Disponível" or "Concluída" or "Entrada" or "Saudável" or "Pago/Recebido" ? Green
            : status is "Pendente" or "Em manutenção" ? Color.FromArgb("#B77C15") : Red;
        return new Border
        {
            Content = Text(status, 10, color, true), Padding = new Thickness(7, 4),
            StrokeThickness = 0, BackgroundColor = color.WithAlpha(0.09f),
            StrokeShape = new RoundRectangle { CornerRadius = 5 }, VerticalOptions = LayoutOptions.Center
        };
    }

    public static View Empty(string text) => Card(Stack(Text("Nenhum registro encontrado", 16, bold: true), Text(text, 13, Muted)));

    public static View Icon(string kind, Color? color = null, double size = 22)
    {
        var data = kind switch
        {
            "home" => "M 3,3 L 9,3 L 9,9 L 3,9 Z M 15,3 L 21,3 L 21,9 L 15,9 Z M 3,15 L 9,15 L 9,21 L 3,21 Z M 15,15 L 21,15 L 21,21 L 15,21 Z",
            "truck" => "M 2,5 L 14,5 L 14,17 L 2,17 Z M 14,9 L 19,9 L 22,13 L 22,17 L 14,17 M 5,17 L 5,20 L 8,20 L 8,17 M 17,17 L 17,20 L 20,20 L 20,17",
            "tool" => "M 14,3 L 13,8 L 17,11 L 22,9 L 21,14 L 17,16 L 13,14 L 6,21 L 3,18 L 10,11 L 9,7 L 11,3 Z",
            "fuel" => "M 3,21 L 3,3 L 14,3 L 14,21 M 1,21 L 16,21 M 5,6 L 12,6 L 12,11 L 5,11 Z M 14,13 L 18,13 L 18,19 L 21,19 L 21,8 L 18,5",
            "money" => "M 12,2 L 12,22 M 18,6 L 8,6 L 5,9 L 8,12 L 16,12 L 19,15 L 16,18 L 5,18",
            _ => "M 8,3 L 16,3 L 18,7 L 16,11 L 8,11 L 6,7 Z M 3,22 L 3,18 L 7,14 L 17,14 L 21,18 L 21,22 Z"
        };
        return new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
            Stroke = new SolidColorBrush(color ?? Blue), StrokeThickness = 1.5,
            WidthRequest = size, HeightRequest = size, Aspect = Stretch.Uniform,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
    }
}
