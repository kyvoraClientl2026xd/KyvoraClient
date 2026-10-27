using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KyvoraPvp;

internal sealed class LaunchLogWindow : Window
{
    private readonly TextBlock _activity;
    private readonly TextBox _output;

    public LaunchLogWindow()
    {
        Title = "Registro de lanzamiento · KyvoraClient";
        Width = 760;
        Height = 460;
        MinWidth = 620;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(10, 8, 19));
        Foreground = Brushes.White;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(27, 15, 39)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(120, 48, 158)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 11, 14, 11)
        };
        var title = new TextBlock
        {
            Text = "KYVORA  /  REGISTRO DE LANZAMIENTO",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(229, 186, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Child = title;
        root.Children.Add(header);

        _activity = new TextBlock
        {
            Text = "Esperando el inicio…",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(191, 162, 220)),
            Margin = new Thickness(4, 12, 4, 12),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(_activity, 1);
        root.Children.Add(_activity);

        _output = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(12, 10, 18)),
            Foreground = new SolidColorBrush(Color.FromRgb(213, 207, 226)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 77)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11
        };
        Grid.SetRow(_output, 2);
        root.Children.Add(_output);
        Content = root;
    }

    public void LoadExisting(string text)
    {
        _output.Text = text;
        _output.ScrollToEnd();
    }

    public void Append(string line)
    {
        _output.AppendText(line + Environment.NewLine);
        _output.ScrollToEnd();
    }

    public void SetActivity(string activity) => _activity.Text = activity;
}
