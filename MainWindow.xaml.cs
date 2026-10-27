using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Net.Http;
using System.Text.Json.Serialization;
using Color = System.Windows.Media.Color;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.ProcessBuilder;
using Microsoft.Identity.Client;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;

namespace KyvoraPvp;

public partial class MainWindow : Window
{
    private const string PremiumVersion = "1.8.9";
    private const string HudVersion1165 = "1.16.5";
    private const string ForgeBuild = "11.15.1.2318";
    private string _selectedVersion = PremiumVersion;
    private string MinecraftDirectory => _selectedVersion == PremiumVersion
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KyvoraClient", "instances", _selectedVersion);
    private const string SetupGuideUrl = "https://learn.microsoft.com/entra/identity-platform/quickstart-register-app";
    private readonly string _clientId;
    private readonly string? _configurationError;
    private MinecraftLauncher _launcher = null!;
    private MSession? _session;
    private DeviceCodePromptWindow? _deviceCodePrompt;
    private LaunchLogWindow? _launchLogWindow;
    private bool _isBusy;
    private int _memoryLimitMb = 1024;

    public MainWindow()
    {
        InitializeComponent();
        _memoryLimitMb = GetRecommendedMemoryMb(_selectedVersion);
        SelectMemoryOption(_memoryLimitMb);
        LoadLauncherSettings();
        _launcher = new MinecraftLauncher(MinecraftDirectory);
        UpdateSelectedVersionDisplay();
        var configuration = LoadClientId();
        _clientId = configuration.ClientId;
        _configurationError = configuration.Error;
        AddLog($"KyvoraClient 26.2 listo. {_selectedVersion} usa Forge y el HUD Kyvora está disponible en 1.8.9 y 1.16.5.");
        if (_configurationError is not null)
        {
            SetStatus("No se pudo leer la configuración", _configurationError, Color.FromRgb(240, 120, 130));
            AddLog(_configurationError);
        }
        else if (string.IsNullOrWhiteSpace(_clientId))
        {
            SetStatus("Falta configurar el Client ID", "Consulta README.md para preparar el inicio de sesión oficial.", Color.FromRgb(231, 185, 92));
            AddLog("Configura MicrosoftClientId en appsettings.json para habilitar el acceso premium.");
        }
        else
        {
            SetStatus("Listo para iniciar sesión", "Inicia sesión con Microsoft para verificar la cuenta.", Color.FromRgb(93, 225, 193));
        }
        UpdatePlayButton();
        Loaded += MainWindow_Loaded;
    }

    private static (string ClientId, string? Error) LoadClientId()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            return (string.Empty, null);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!document.RootElement.TryGetProperty("MicrosoftClientId", out var clientId))
                return (string.Empty, null);

            if (clientId.ValueKind != JsonValueKind.String)
                return (string.Empty, "MicrosoftClientId debe ser un texto en appsettings.json.");

            return (clientId.GetString()?.Trim() ?? string.Empty, null);
        }
        catch (JsonException)
        {
            return (string.Empty, "appsettings.json no contiene JSON válido.");
        }
        catch (IOException)
        {
            return (string.Empty, "No se pudo leer appsettings.json.");
        }
    }

    private void LoadLauncherSettings()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KyvoraClient", "launcher-settings.json");
            if (!File.Exists(path)) return;
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.TryGetProperty("MinecraftVersion", out var selectedVersion) && selectedVersion.ValueKind == JsonValueKind.String)
            {
                var version = selectedVersion.GetString();
                if (!string.IsNullOrWhiteSpace(version) && version.Length <= 32 && version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
                    _selectedVersion = version;
            }
            if (!json.RootElement.TryGetProperty("MemoryMb", out var ram) || !ram.TryGetInt32(out var value)) return;
            _memoryLimitMb = ClampMemoryToSafeLimit(value);
            for (var i = 0; i < MemorySelector.Items.Count; i++)
                if (MemorySelector.Items[i] is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var option) && option == _memoryLimitMb)
                { MemorySelector.SelectedIndex = i; break; }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        { AddLog($"Se usarán los ajustes predeterminados: {ex.Message}"); }
    }

    private async void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        if (string.IsNullOrWhiteSpace(_clientId))
        {
            var setupError = _configurationError ?? "Abre README.md y sigue la guía de configuración de Microsoft.";
            SetStatus(_configurationError is null ? "Falta configurar el Client ID" : "Revisa la configuración", setupError, Color.FromRgb(231, 185, 92));
            AddLog($"No se inició el acceso: {setupError}");
            try
            {
                Process.Start(new ProcessStartInfo(SetupGuideUrl) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                AddLog($"No se pudo abrir la guía en el navegador ({exception.GetType().Name}).");
            }
            return;
        }

        var previousSession = _session;
        SetBusy(true);
        AccountButton.Content = "Conectando...";
        SetStatus("Esperando a Microsoft", "Completa el acceso en la ventana segura que se abrirá.", Color.FromRgb(231, 185, 92));
        AddLog("Abriendo el inicio de sesión oficial de Microsoft...");

        try
        {
            var loginHandler = await CreateLoginHandlerAsync(useDeviceCode: true);
            _session = await loginHandler.AuthenticateInteractively();
            CloseDeviceCodePrompt();
            SetAuthenticatedSession(_session);
            AddLog($"Autenticación Microsoft completada para {_session.Username}.");
        }
        catch (Exception exception)
        {
            CloseDeviceCodePrompt();
            _session = previousSession;
            AccountButton.Content = previousSession is null ? "Vincular cuenta Microsoft" : "Cambiar cuenta Microsoft";
            var errorDetail = DescribeAuthenticationFailure(exception);
            SetStatus("No se pudo iniciar sesión", errorDetail, Color.FromRgb(240, 120, 130));
            AddLog($"Falló la autenticación Microsoft ({exception.GetType().Name}): {exception.Message}");
            MessageBox.Show(
                this,
                $"Microsoft no pudo completar el acceso a Minecraft.\n\n{errorDetail}\n\nConfirma que usaste una cuenta con Minecraft Java y que la configuración de la aplicación Microsoft está correcta.",
                "No se pudo conectar la cuenta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
            UpdatePlayButton();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AnimateEntrance();
        if (string.IsNullOrWhiteSpace(_clientId) || _configurationError is not null)
            return;

        SetBusy(true);
        AccountButton.Content = "Restaurando sesión...";
        SetStatus("Comprobando sesión guardada", "Buscando una cuenta Microsoft conectada anteriormente.", Color.FromRgb(231, 185, 92));
        AddLog("Comprobando si MSAL puede restaurar una sesión guardada...");
        try
        {
            var loginHandler = await CreateLoginHandlerAsync();
            _session = await loginHandler.AuthenticateSilently();
            SetAuthenticatedSession(_session);
            AddLog($"Sesión Microsoft restaurada para {_session.Username}.");
        }
        catch (MsalUiRequiredException)
        {
            _session = null;
            AccountButton.Content = "Vincular cuenta Microsoft";
            SetStatus("Listo para iniciar sesión", "Pulsa “Vincular cuenta Microsoft” para continuar.", Color.FromRgb(93, 225, 193));
            AddLog("No existe una sesión reutilizable. Se necesita iniciar sesión desde el launcher.");
        }
        catch (Exception exception)
        {
            _session = null;
            AccountButton.Content = "Vincular cuenta Microsoft";
            SetStatus("No se pudo restaurar la sesión", DescribeAuthenticationFailure(exception), Color.FromRgb(240, 120, 130));
            AddLog($"Falló la restauración de sesión ({exception.GetType().Name}): {exception.Message}");
        }
        finally
        {
            SetBusy(false);
            UpdatePlayButton();
        }
    }

    private async Task<JELoginHandler> CreateLoginHandlerAsync()
    {
        var application = await BuildMicrosoftApplicationWithCacheAsync();
        return new JELoginHandlerBuilder()
            .WithOAuthProvider(new MsalCodeFlowProvider(application))
            .Build();
    }

    private async Task<JELoginHandler> CreateLoginHandlerAsync(bool useDeviceCode)
    {
        if (!useDeviceCode)
            return await CreateLoginHandlerAsync();

        var application = await BuildMicrosoftApplicationWithCacheAsync();
        var provider = new MsalDeviceCodeProvider(application, deviceCode =>
        {
            Dispatcher.Invoke(() => ShowDeviceCodePrompt(deviceCode));
            return Task.CompletedTask;
        });
        return new JELoginHandlerBuilder()
            .WithOAuthProvider(provider)
            .Build();
    }

    private async Task<Microsoft.Identity.Client.IPublicClientApplication> BuildMicrosoftApplicationWithCacheAsync()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDirectory = Path.Combine(localAppData, "KyvoraClient", "auth");
        const string cacheFileName = "kyvora_msal_cache.txt";
        var cachePath = Path.Combine(cacheDirectory, cacheFileName);
        Directory.CreateDirectory(cacheDirectory);

        var legacyCachePath = Path.Combine(localAppData, "cmllib_msal_cache.txt");
        if (!File.Exists(cachePath) && File.Exists(legacyCachePath))
        {
            try
            {
                File.Copy(legacyCachePath, cachePath);
                AddLog("Se conservó la cuenta Microsoft vinculada anteriormente.");
            }
            catch (IOException exception)
            {
                AddLog($"No se pudo migrar la caché de inicio de sesión ({exception.GetType().Name}); se creará una nueva al iniciar sesión.");
            }
            catch (UnauthorizedAccessException exception)
            {
                AddLog($"Windows no permitió migrar la caché de inicio de sesión ({exception.GetType().Name}); se creará una nueva al iniciar sesión.");
            }
        }

        var cacheSettings = new MsalCacheSettings
        {
            CacheFileName = cacheFileName,
            CacheDir = cacheDirectory
        };
        return await MsalClientHelper.BuildApplicationWithCache(_clientId, cacheSettings);
    }

    private void ShowDeviceCodePrompt(DeviceCodeResult deviceCode)
    {
        CloseDeviceCodePrompt();
        _deviceCodePrompt = new DeviceCodePromptWindow(deviceCode)
        {
            Owner = this
        };
        _deviceCodePrompt.Closed += (_, _) => _deviceCodePrompt = null;
        _deviceCodePrompt.Show();

        try
        {
            Process.Start(new ProcessStartInfo(deviceCode.VerificationUrl)
            {
                UseShellExecute = true
            });
            AddLog($"Abre {deviceCode.VerificationUrl} e escribe el código mostrado para aprobar el acceso.");
            SetStatus("Aprueba el acceso de Microsoft", "Escribe el código en la página abierta; KyvoraClient esperará la confirmación.", Color.FromRgb(231, 185, 92));
        }
        catch (Exception exception)
        {
            AddLog($"No se pudo abrir automáticamente la página de acceso ({exception.GetType().Name}): {exception.Message}");
            SetStatus($"Abre {deviceCode.VerificationUrl}", "Usa el código que aparece en la ventana de acceso de KyvoraClient.", Color.FromRgb(231, 185, 92));
        }
    }

    private void CloseDeviceCodePrompt()
    {
        if (_deviceCodePrompt is null)
            return;

        _deviceCodePrompt.Close();
        _deviceCodePrompt = null;
    }

    private void SetAuthenticatedSession(MSession session)
    {
        GreetingText.Text = $"Hola, {session.Username}";
        AccountButton.Content = "Cambiar cuenta Microsoft";
        SetStatus("Cuenta premium verificada", $"Conectado como {session.Username}.", Color.FromRgb(93, 225, 193));
    }

    private static string DescribeAuthenticationFailure(Exception exception)
    {
        var msalException = FindMsalServiceException(exception);
        if (msalException is not null)
        {
            var details = new List<string>
            {
                $"Código MSAL: {msalException.ErrorCode}",
                $"HTTP: {msalException.StatusCode}"
            };
            if (TryReadMsalErrorDescription(msalException.ResponseBody) is { Length: > 0 } description)
                details.Add(description);
            return string.Join(Environment.NewLine, details);
        }

        var detail = exception.Message.Trim();
        if (detail.Length > 220)
            detail = detail[..220] + "...";
        return string.IsNullOrWhiteSpace(detail)
            ? $"Error {exception.GetType().Name}. Revisa tu cuenta Microsoft y vuelve a intentarlo."
            : detail;
    }

    private static MsalServiceException? FindMsalServiceException(Exception exception)
    {
        if (exception is MsalServiceException msalException)
            return msalException;

        if (exception is AggregateException aggregate)
        {
            foreach (var innerException in aggregate.Flatten().InnerExceptions)
            {
                var found = FindMsalServiceException(innerException);
                if (found is not null)
                    return found;
            }
        }

        return exception.InnerException is null
            ? null
            : FindMsalServiceException(exception.InnerException);
    }

    private static string? TryReadMsalErrorDescription(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var response = JsonDocument.Parse(responseBody);
            if (response.RootElement.TryGetProperty("error_description", out var description) &&
                description.ValueKind == JsonValueKind.String)
                return description.GetString();

            if (response.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
                return error.GetString();
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || _isBusy)
            return;

        ShowLaunchLogs();
        SetBusy(true);
        InstallProgress.Visibility = Visibility.Visible;
        ProgressText.Visibility = Visibility.Visible;
        InstallProgress.Value = 0;
        var gameVersion = _selectedVersion;
        var hudJarName = gameVersion switch
        {
            PremiumVersion => "KyvoraHud.jar",
            HudVersion1165 => "KyvoraHud-1.16.5.jar",
            _ => null
        };
        var includesKyvoraHud = hudJarName is not null;
        var readyMessage = includesKyvoraHud
            ? $"Forge {gameVersion} y HUD Kyvora están listos."
            : $"Forge {gameVersion} está listo.";
        _launcher = new MinecraftLauncher(MinecraftDirectory);
        PlayButton.Content = $"PREPARANDO {gameVersion}...";
        SetStatus("Preparando el cliente", includesKyvoraHud ? $"Configurando Forge {gameVersion}, HUD Kyvora y los archivos oficiales." : $"Configurando Forge {gameVersion} y los archivos oficiales.", Color.FromRgb(231, 185, 92));
        AddLog($"Preparando Minecraft {gameVersion} con Forge...");

        EventHandler<CmlLib.Core.Installers.InstallerProgressChangedEventArgs> fileHandler = (_, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (args.TotalTasks > 0)
                    InstallProgress.Value = args.ProgressedTasks * 100d / args.TotalTasks;
                ProgressText.Text = string.IsNullOrWhiteSpace(args.Name)
                    ? $"Preparando archivos: {args.ProgressedTasks}/{args.TotalTasks}"
                    : $"Preparando: {args.Name}";
                _launchLogWindow?.SetActivity(ProgressText.Text);
            });
        };

        _launcher.FileProgressChanged += fileHandler;
        try
        {
            var hudSource = hudJarName is null ? null : Path.Combine(AppContext.BaseDirectory, "mods", hudJarName);
            if (hudSource is not null && !File.Exists(hudSource))
                throw new FileNotFoundException($"No se encontró {hudJarName}. Reinstala KyvoraClient con el instalador actualizado.", hudSource);

            SetStatus("Instalando Minecraft", $"Descargando los archivos oficiales de Minecraft {gameVersion}.", Color.FromRgb(231, 185, 92));
            await _launcher.InstallAsync(gameVersion);
            var launchVersion = FindForgeProfile(gameVersion);
            if (launchVersion is null)
            {
                SetStatus("Instalando Forge", $"Buscando e instalando Forge para Minecraft {gameVersion}.", Color.FromRgb(231, 185, 92));
                await InstallForgeAsync(gameVersion);
                launchVersion = FindForgeProfile(gameVersion)
                    ?? throw new InvalidOperationException($"Forge terminó, pero no se encontró el perfil de Minecraft {gameVersion}.");
            }
            await _launcher.InstallAsync(launchVersion);

            if (includesKyvoraHud)
            {
                var modsDirectory = Path.Combine(MinecraftDirectory, "mods");
                Directory.CreateDirectory(modsDirectory);
                File.Copy(hudSource!, Path.Combine(modsDirectory, hudJarName!), overwrite: true);
            }

            SetStatus("Iniciando Minecraft", readyMessage, Color.FromRgb(93, 225, 193));
            AddLog($"Minecraft {gameVersion} preparado. {readyMessage} Iniciando Minecraft.");
            var launchOptions = new MLaunchOption
            {
                Session = _session,
                MaximumRamMb = _memoryLimitMb,
                MinimumRamMb = 512,
                JavaPath = GetBundledJavaPath("javaw.exe")
            };
            var process = await _launcher.BuildProcessAsync(launchVersion, launchOptions);
            if (!process.Start())
                throw new InvalidOperationException("Windows no pudo iniciar el proceso de Minecraft.");

            AddLog($"Minecraft iniciado correctamente (PID {process.Id}).");
            SetStatus("Minecraft está en ejecución", includesKyvoraHud ? "Pulsa Shift derecho para configurar Keystrokes, FPS y otros HUD." : readyMessage, Color.FromRgb(93, 225, 193));
            InstallProgress.Value = 100;
            ProgressText.Text = "¡Listo!";
        }
        catch (Exception exception)
        {
            SetStatus("No se pudo iniciar Minecraft", "Revisa el registro y confirma que la cuenta tenga Minecraft Java.", Color.FromRgb(240, 120, 130));
            AddLog($"Error al preparar o iniciar Minecraft ({exception.GetType().Name}): {exception.Message}");
        }
        finally
        {
            _launcher.FileProgressChanged -= fileHandler;
            SetBusy(false);
            UpdatePlayButton();
            PlayButton.Content = "▶   JUGAR AHORA";
        }
    }

    private async Task InstallForgeAsync(string gameVersion)
    {
        var forgeCatalog = new ForgeInstaller(_launcher);
        var availableVersions = await forgeCatalog.GetForgeVersions(gameVersion);
        var selectedVersion = gameVersion == PremiumVersion
            ? availableVersions.FirstOrDefault(version => version.ForgeVersionName == ForgeBuild)
            : availableVersions.FirstOrDefault();
        if (selectedVersion is null)
            throw new InvalidOperationException($"No se encontró Forge compatible con Minecraft {gameVersion}.");

        var installer = new ForgeInstallerVersionMapper().CreateInstaller(selectedVersion);
        var options = new ForgeInstallOptions
        {
            JavaPath = GetBundledJavaPath("java.exe") ?? "java.exe",
            RulesContext = _launcher.RulesContext,
            RulesEvaluator = _launcher.RulesEvaluator,
            FileProgress = new Progress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>(args =>
            {
                if (args.TotalTasks > 0) InstallProgress.Value = args.ProgressedTasks * 100d / args.TotalTasks;
                ProgressText.Text = string.IsNullOrWhiteSpace(args.Name)
                    ? $"Instalando Forge: {args.ProgressedTasks}/{args.TotalTasks}"
                    : $"Forge: {args.Name}";
                _launchLogWindow?.SetActivity(ProgressText.Text);
            }),
            InstallerOutput = new Progress<string>(AddLog)
        };
        await installer.Install(_launcher.MinecraftPath, _launcher.GameInstaller, options);
    }

    private string? FindForgeProfile(string gameVersion)
    {
        var versionsDirectory = Path.Combine(MinecraftDirectory, "versions");
        if (!Directory.Exists(versionsDirectory)) return null;
        if (gameVersion == PremiumVersion)
        {
            var knownProfile = $"{PremiumVersion}-forge{PremiumVersion}-{ForgeBuild}-{PremiumVersion}";
            if (File.Exists(Path.Combine(versionsDirectory, knownProfile, knownProfile + ".json"))) return knownProfile;
        }
        foreach (var directory in Directory.EnumerateDirectories(versionsDirectory))
        {
            var profileId = Path.GetFileName(directory);
            if (!profileId.StartsWith(gameVersion + "-forge", StringComparison.OrdinalIgnoreCase)) continue;
            var profilePath = Path.Combine(directory, profileId + ".json");
            if (!File.Exists(profilePath)) continue;
            try
            {
                using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
                if (!profile.RootElement.TryGetProperty("inheritsFrom", out var parent) || parent.GetString() == gameVersion)
                    return profileId;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            { AddLog($"No se pudo leer el perfil Forge {profileId}: {ex.Message}"); }
        }
        return null;
    }

    private static string? GetBundledJavaPath(string executableName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "runtime", "bin", executableName);
        return File.Exists(path) ? path : null;
    }

    private void MemorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MemorySelector.SelectedItem is not ComboBoxItem item ||
            !int.TryParse(item.Tag?.ToString(), out var requestedMb))
            return;

        var safeLimit = GetSafeMemoryLimitMb();
        if (requestedMb > safeLimit)
        {
            _memoryLimitMb = safeLimit;
            SelectMemoryOption(_memoryLimitMb);
            AddLog($"RAM ajustada automáticamente a {_memoryLimitMb / 1024d:0.#} GB para dejar recursos al sistema.");
        }
        else
        {
            _memoryLimitMb = requestedMb;
        }
    }

    private static int GetSafeMemoryLimitMb()
    {
        var availableMb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024));
        var memoryCeiling = Math.Max(1024, (int)(availableMb * 0.45));
        return new[] { 1024, 1536, 2048, 3072, 4096, 5120, 6144, 8192 }
            .Where(value => value <= memoryCeiling).DefaultIfEmpty(1024).Max();
    }

    private static int GetRecommendedMemoryMb(string version)
    {
        var availableMb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024));
        var minor = ParseMinorVersion(version);
        var recommendation = minor <= 12 ? 1024 : minor <= 14 ? 1536 : 2048;
        if (availableMb <= 4096) recommendation = 1024;
        else if (availableMb <= 6144) recommendation = Math.Min(recommendation, 1536);
        return Math.Min(recommendation, GetSafeMemoryLimitMb());
    }

    private static int ParseMinorVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 16;
    }

    private static int ClampMemoryToSafeLimit(int requestedMb)
    {
        var options = new[] { 1024, 1536, 2048, 3072, 4096, 5120, 6144, 8192 };
        var safeLimit = GetSafeMemoryLimitMb();
        return options.Where(value => value <= requestedMb && value <= safeLimit).DefaultIfEmpty(1024).Max();
    }

    private void SelectMemoryOption(int memoryMb)
    {
        for (var i = 0; i < MemorySelector.Items.Count; i++)
            if (MemorySelector.Items[i] is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var option) && option == memoryMb)
            { MemorySelector.SelectedIndex = i; return; }
    }

    private void HomeNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(HomeNavButton);
        MainContentScrollViewer.ScrollToTop();
    }

    private void StatusNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNav(StatusNavButton);
        GameStatusPanel.BringIntoView();
    }

    private void OpenFolderNavButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(MinecraftDirectory);
            Process.Start(new ProcessStartInfo(MinecraftDirectory) { UseShellExecute = true });
            SetActiveNav(FolderNavButton);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { AddLog($"No se pudo abrir la carpeta de la instancia: {ex.Message}"); }
    }

    private void VersionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        SetActiveNav(VersionNavButton);
        var previousVersion = _selectedVersion;
        var picker = new VersionPickerWindow(_selectedVersion) { Owner = this };
        if (picker.ShowDialog() != true || string.IsNullOrWhiteSpace(picker.SelectedVersion)) return;
        _selectedVersion = picker.SelectedVersion;
        if (_memoryLimitMb == GetRecommendedMemoryMb(previousVersion))
        {
            _memoryLimitMb = GetRecommendedMemoryMb(_selectedVersion);
            SelectMemoryOption(_memoryLimitMb);
        }
        _launcher = new MinecraftLauncher(MinecraftDirectory);
        UpdateSelectedVersionDisplay();
        SaveLauncherSettings();
        SetStatus($"Versión { _selectedVersion } seleccionada", $"Se preparará Forge para Minecraft {_selectedVersion}; el HUD Kyvora se incluye en 1.8.9 y 1.16.5.", Color.FromRgb(93, 225, 193));
        AddLog($"Versión de Minecraft cambiada a {_selectedVersion}.");
    }

    private void UpdateSelectedVersionDisplay()
    {
        SelectedVersionText.Text = _selectedVersion;
        GameVersionDisplay.Text = _selectedVersion is PremiumVersion or HudVersion1165
            ? $"{_selectedVersion}  ·  KYVORA HUD"
            : $"{_selectedVersion}  ·  FORGE";
        SidebarVersionText.Text = $"Minecraft {_selectedVersion}";
        SidebarInstancePath.Text = MinecraftDirectory;
        LauncherTagline.Text = $"✦  KYVORA 26.2  ·  CLIENTE PVP PARA MINECRAFT {_selectedVersion}";
        HeroVersionText.Text = _selectedVersion is PremiumVersion or HudVersion1165
            ? $"KYVORA PVP  /  CLIENTE PARA {_selectedVersion}"
            : $"MINECRAFT JAVA  /  VERSIÓN {_selectedVersion}";
        VersionNavButton.ToolTip = $"Versión actual: {_selectedVersion}. Pulsa para cambiar.";
        SelectedProfileText.Text = _selectedVersion is PremiumVersion or HudVersion1165
            ? $"KYVORA PVP · {_selectedVersion}"
            : $"Forge · {_selectedVersion}";
        SelectedInstanceDetail.Text = _selectedVersion == PremiumVersion
            ? $"Forge {ForgeBuild} · HUD Kyvora"
            : _selectedVersion == HudVersion1165 ? "Forge · HUD Kyvora" : "Perfil Forge · instancia independiente";
    }

    private void SaveLauncherSettings()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KyvoraClient", "launcher-settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { MemoryMb = _memoryLimitMb, MinecraftVersion = _selectedVersion }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { AddLog($"No se pudieron guardar los ajustes: {ex.Message}"); }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        SetActiveNav(SettingsNavButton);
        var dialog = new Window
        {
            Title = "Ajustes de Kyvora", Owner = this, Width = 500, Height = 370,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(17, 19, 25)),
            Foreground = Brushes.White, ResizeMode = ResizeMode.NoResize
        };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "AJUSTES DEL CLIENTE", FontSize = 18, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = _selectedVersion == PremiumVersion ? $"Minecraft {_selectedVersion} · Forge {ForgeBuild}" : $"Minecraft {_selectedVersion} · Forge", Margin = new Thickness(0, 6, 0, 20), Foreground = Brushes.LightGray });
        panel.Children.Add(new TextBlock { Text = "Memoria máxima para Minecraft", FontWeight = FontWeights.SemiBold });
        var memory = new ComboBox { Margin = new Thickness(0, 8, 0, 16), Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var value in new[] { 1024, 1536, 2048, 3072, 4096, 5120, 6144, 8192 })
            memory.Items.Add(new ComboBoxItem { Content = $"{value / 1024d:0.#} GB", Tag = value });
        memory.SelectedIndex = Math.Max(0, Enumerable.Range(0, memory.Items.Count)
            .FirstOrDefault(i => (int)((ComboBoxItem)memory.Items[i]).Tag == _memoryLimitMb));
        panel.Children.Add(memory);
        panel.Children.Add(new TextBlock { Text = $"Carpeta del juego: {MinecraftDirectory}", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 18) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var openFolder = new Button { Content = "ABRIR CARPETA", Background = new SolidColorBrush(Color.FromRgb(36, 40, 51)), Margin = new Thickness(0, 0, 8, 0) };
        openFolder.Click += (_, _) => Process.Start(new ProcessStartInfo(MinecraftDirectory) { UseShellExecute = true });
        var save = new Button { Content = "GUARDAR", Background = new SolidColorBrush(Color.FromRgb(186, 72, 255)) };
        save.Click += (_, _) =>
        {
            if (memory.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var selected))
            {
                _memoryLimitMb = ClampMemoryToSafeLimit(selected);
                SelectMemoryOption(_memoryLimitMb);
                SaveLauncherSettings();
            }
            dialog.Close();
        };
        actions.Children.Add(openFolder); actions.Children.Add(save); panel.Children.Add(actions);
        dialog.Content = panel;
        dialog.ShowDialog();
    }

    private void UpdatePlayButton()
    {
        PlayButton.IsEnabled = !_isBusy && _session is not null;
        AccountButton.IsEnabled = !_isBusy;
        if (!_isBusy && _session is not null)
            AccountButton.Content = "Cambiar cuenta Microsoft";
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        AccountButton.IsEnabled = !isBusy && _session is null;
        PlayButton.IsEnabled = !isBusy && _session is not null;
        MemorySelector.IsEnabled = !isBusy;
        VersionNavButton.IsEnabled = !isBusy;
    }

    private void SetActiveNav(Button active)
    {
        foreach (var button in new[] { HomeNavButton, VersionNavButton, FolderNavButton, StatusNavButton, SettingsNavButton })
        {
            button.Background = button == active
                ? new SolidColorBrush(Color.FromRgb(41, 17, 58))
                : Brushes.Transparent;
            button.BorderBrush = button == active
                ? new SolidColorBrush(Color.FromRgb(184, 71, 255))
                : new SolidColorBrush(Color.FromRgb(51, 38, 66));
            button.BorderThickness = button == active ? new Thickness(1.5) : new Thickness(1);
            button.Foreground = button == active
                ? new SolidColorBrush(Color.FromRgb(240, 215, 255))
                : new SolidColorBrush(Color.FromRgb(174, 180, 194));
        }
    }

    private void SetStatus(string title, string detail, Color color)
    {
        StatusText.Text = title;
        StatusDetailText.Text = detail;
        StatusIndicator.Fill = new SolidColorBrush(color);
        _launchLogWindow?.SetActivity(title);
    }

    private void AnimateEntrance()
    {
        var slide = new TranslateTransform(0, 16);
        RootLayout.RenderTransform = slide;
        RootLayout.Opacity = 0;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        RootLayout.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(320)))
        {
            EasingFunction = easing
        });
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(16, 0, new Duration(TimeSpan.FromMilliseconds(360))) { EasingFunction = easing });
    }

    private void ShowLaunchLogs()
    {
        if (_launchLogWindow is { IsVisible: true })
        {
            _launchLogWindow.Activate();
            return;
        }

        var window = new LaunchLogWindow { Owner = this };
        window.LoadExisting(LogText.Text);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_launchLogWindow, window))
                _launchLogWindow = null;
        };
        _launchLogWindow = window;
        window.Show();
    }

    private void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogText.AppendText(line + Environment.NewLine);
        LogText.ScrollToEnd();
        _launchLogWindow?.Append(line);
    }
}
