using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KyvoraPvp;

internal sealed class ModBrowserWindow : Window
{
    private static readonly HttpClient Http = CreateClient();
    private readonly string _gameDirectory;
    private readonly string _gameVersion;
    private readonly TextBox _search = new() { Width = 310, Height = 34, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _source = new() { Width = 150, Height = 34, Margin = new Thickness(8, 0, 0, 0) };
    private readonly ListBox _results = new() { Background = Brush("#111824"), Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 14, 0, 0) };
    private readonly TextBlock _status = new() { Text = "Busca mods compatibles con Forge.", Foreground = Brush("#AAB3C2"), Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _install = new() { Content = "INSTALAR MOD SELECCIONADO", Background = Brush("#8C7BFF"), Margin = new Thickness(0, 10, 0, 0), IsEnabled = false };
    private readonly Button _installed = new() { Content = "MIS MODS", Background = Brush("#242833"), Margin = new Thickness(8, 0, 0, 0) };
    private List<ModResult> _items = [];

    public ModBrowserWindow(string gameDirectory, string gameVersion)
    {
        _gameDirectory = gameDirectory;
        _gameVersion = gameVersion;
        Title = $"Mods · Minecraft {gameVersion} · Kyvora 26.2"; Width = 850; Height = 650; MinWidth = 700; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Brush("#090D15"); Foreground = Brushes.White;
        var root = new DockPanel { Margin = new Thickness(22) };
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "CATÁLOGO DE MODS", FontSize = 20, FontWeight = FontWeights.Bold });
        heading.Children.Add(new TextBlock { Text = $"Modrinth · Minecraft {gameVersion} · Forge", Foreground = Brush("#AAB3C2"), Margin = new Thickness(0, 4, 0, 14) });
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        _search.Text = ""; _search.ToolTip = "Nombre o descripción";
        searchRow.Children.Add(_search);
        _source.Items.Add("Todos"); _source.Items.Add("Rendimiento"); _source.Items.Add("Utilidades"); _source.Items.Add("PvP"); _source.SelectedIndex = 0;
        searchRow.Children.Add(_source);
        var searchButton = new Button { Content = "BUSCAR", Background = Brush("#242833"), Margin = new Thickness(8, 0, 0, 0) };
        searchButton.Click += async (_, _) => await SearchAsync();
        searchRow.Children.Add(searchButton); searchRow.Children.Add(_installed);
        heading.Children.Add(searchRow);
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        DockPanel.SetDock(_install, Dock.Bottom); root.Children.Add(_install);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(_results); Content = root;
        _results.SelectionChanged += (_, _) => _install.IsEnabled = _results.SelectedIndex >= 0;
        _install.Click += async (_, _) => await InstallSelectedAsync();
        _installed.Click += (_, _) => ShowInstalled();
        _search.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) await SearchAsync(); };
    }

    private async Task SearchAsync()
    {
        _status.Text = "Buscando en Modrinth…"; _results.Items.Clear(); _items.Clear(); _install.IsEnabled = false;
        try
        {
            var query = Uri.EscapeDataString(_search.Text.Trim());
            var category = _source.SelectedIndex switch { 1 => "performance", 2 => "utility", 3 => "equipment", _ => "" };
            var facets = $"[[\"project_type:mod\"],[\"versions:{_gameVersion}\"],[\"categories:forge\"]" + (category.Length == 0 ? "" : $",[\"categories:{category}\"]") + "]";
            var url = $"https://api.modrinth.com/v2/search?query={query}&facets={Uri.EscapeDataString(facets)}&limit=30&index=relevance";
            using var response = await Http.GetAsync(url); response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var hit in json.RootElement.GetProperty("hits").EnumerateArray())
                _items.Add(new ModResult(hit.GetProperty("project_id").GetString()!, hit.GetProperty("title").GetString() ?? "Mod",
                    hit.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    hit.TryGetProperty("downloads", out var downloads) ? downloads.GetInt32() : 0,
                    hit.TryGetProperty("slug", out var slug) ? slug.GetString() ?? "" : ""));
            foreach (var mod in _items)
                _results.Items.Add($"{mod.Title}   ·   {mod.Downloads:N0} descargas\n{mod.Description}");
            _status.Text = _items.Count == 0 ? "No se encontraron mods con esos filtros. Prueba otra búsqueda." : $"{_items.Count} mods compatibles. Selecciona uno para instalar.";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        { _status.Text = $"No se pudo consultar Modrinth: {ex.Message}"; }
    }

    private async Task InstallSelectedAsync()
    {
        if (_results.SelectedIndex < 0 || _results.SelectedIndex >= _items.Count) return;
        var mod = _items[_results.SelectedIndex]; _install.IsEnabled = false;
        try
        {
            _status.Text = $"Buscando versión de {mod.Title}…";
            var url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(mod.Id)}/version?game_versions=%5B%22{Uri.EscapeDataString(_gameVersion)}%22%5D&loaders=%5B%22forge%22%5D";
            using var response = await Http.GetAsync(url); response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var version = json.RootElement.EnumerateArray().FirstOrDefault();
            if (version.ValueKind != JsonValueKind.Object || !version.TryGetProperty("files", out var files) || files.GetArrayLength() == 0)
                throw new InvalidOperationException($"El proyecto no ofrece un archivo instalable para Forge en Minecraft {_gameVersion}.");
            var file = files.EnumerateArray().FirstOrDefault(f => f.TryGetProperty("primary", out var primary) && primary.GetBoolean());
            if (file.ValueKind != JsonValueKind.Object) file = files[0];
            var download = file.GetProperty("url").GetString()!;
            var filename = Path.GetFileName(file.GetProperty("filename").GetString()!);
            var destination = Path.Combine(_gameDirectory, "mods"); Directory.CreateDirectory(destination);
            _status.Text = $"Descargando {mod.Title}…";
            var bytes = await Http.GetByteArrayAsync(download);
            await File.WriteAllBytesAsync(Path.Combine(destination, filename), bytes);
            _status.Text = $"{mod.Title} instalado en la instancia de Minecraft {_gameVersion}. Revisa sus dependencias en Modrinth.";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException or InvalidOperationException or TaskCanceledException)
        { _status.Text = $"No se pudo instalar: {ex.Message}"; }
        finally { _install.IsEnabled = _results.SelectedIndex >= 0; }
    }

    private void ShowInstalled()
    {
        var folder = Path.Combine(_gameDirectory, "mods"); Directory.CreateDirectory(folder);
        _items.Clear(); _results.Items.Clear();
        foreach (var file in Directory.EnumerateFiles(folder, "*.jar"))
            _results.Items.Add(Path.GetFileName(file));
        _status.Text = $"{_results.Items.Count} archivos .jar en {folder}. Para desinstalar, bórralos de esa carpeta.";
        _install.IsEnabled = false;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KyvoraClient/26.2 (Forge mod browser)");
        return client;
    }

    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private sealed record ModResult(string Id, string Title, string Description, int Downloads, string Slug);
}
