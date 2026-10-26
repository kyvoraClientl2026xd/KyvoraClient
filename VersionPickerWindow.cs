using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KyvoraPvp;

internal sealed class VersionPickerWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly TextBox _filter = new() { Height = 34, Margin = new Thickness(0, 12, 0, 10), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ListBox _list = new() { Background = new SolidColorBrush(Color.FromRgb(17, 24, 36)), Foreground = Brushes.White };
    private readonly TextBlock _status = new() { Text = "Cargando versiones oficiales…", Foreground = Brushes.LightGray, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Button _choose = new() { Content = "USAR ESTA VERSIÓN", IsEnabled = false, Background = new SolidColorBrush(Color.FromRgb(140, 123, 255)) };
    private readonly List<string> _versions = [];
    public string? SelectedVersion { get; private set; }

    public VersionPickerWindow(string currentVersion)
    {
        Title = "Cambiar versión · Kyvora 26.2"; Width = 460; Height = 620; MinWidth = 400; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(9, 13, 21)); Foreground = Brushes.White;
        var root = new DockPanel { Margin = new Thickness(20) };
        var head = new StackPanel();
        head.Children.Add(new TextBlock { Text = "ELIGE TU VERSIÓN", FontSize = 18, FontWeight = FontWeights.Bold });
        head.Children.Add(new TextBlock { Text = "Versiones oficiales compatibles con Java 8 · hasta 1.16.5", Foreground = Brushes.LightGray, Margin = new Thickness(0, 4, 0, 0) });
        _filter.ToolTip = "Filtrar, por ejemplo: 1.8.9 o 1.20";
        head.Children.Add(_filter);
        DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
        var footer = new StackPanel();
        footer.Children.Add(_status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "CANCELAR", Background = new SolidColorBrush(Color.FromRgb(36, 40, 51)), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        _choose.Click += (_, _) => { if (_list.SelectedItem is string selected) { SelectedVersion = selected; DialogResult = true; } };
        buttons.Children.Add(cancel); buttons.Children.Add(_choose); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(_list); Content = root;
        _list.SelectionChanged += (_, _) => _choose.IsEnabled = _list.SelectedItem is string;
        _filter.TextChanged += (_, _) => RefreshList(currentVersion);
        Loaded += async (_, _) => await LoadVersionsAsync(currentVersion);
    }

    private async Task LoadVersionsAsync(string currentVersion)
    {
        try
        {
            using var response = await Http.GetAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var item in json.RootElement.GetProperty("versions").EnumerateArray())
                if (item.GetProperty("type").GetString() == "release" && IsJava8Compatible(item.GetProperty("id").GetString()!))
                    _versions.Add(item.GetProperty("id").GetString()!);
            RefreshList(currentVersion);
            _status.Text = $"{_versions.Count} versiones oficiales compatibles con Java 8. HUD Kyvora en 1.8.9 y 1.16.5.";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        { _status.Text = $"No se pudo cargar el catálogo oficial: {ex.Message}"; }
    }

    private static bool IsJava8Compatible(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 && parts[0] == "1" && int.TryParse(parts[1], out var minor) && minor <= 16;
    }

    private void RefreshList(string currentVersion)
    {
        var query = _filter.Text.Trim();
        var matches = _versions.Where(v => v.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _list.ItemsSource = matches;
        if (matches.Contains(currentVersion)) _list.SelectedItem = currentVersion;
    }
}
