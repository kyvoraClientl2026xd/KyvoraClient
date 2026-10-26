using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KyvoraSetup;

internal sealed class InstallerForm : Form
{
    private readonly Label _status;
    private readonly Label _detail;
    private readonly ProgressBar _progress;
    private readonly Button _finish;
    private readonly string _installDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "KyvoraClient");

    public InstallerForm()
    {
        Text = "KyvoraClient Installer";
        ClientSize = new Size(560, 330);
        MinimumSize = new Size(560, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(11, 13, 20);
        ForeColor = Color.FromArgb(245, 246, 250);
        Font = new Font("Segoe UI", 9F);
        using (var backgroundStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Kyvora-Galaxy.jpg"))
        {
            if (backgroundStream is not null)
            {
                using var sourceImage = Image.FromStream(backgroundStream);
                BackgroundImage = new Bitmap(sourceImage);
                BackgroundImageLayout = ImageLayout.Stretch;
                Paint += (_, args) =>
                {
                    using var overlay = new SolidBrush(Color.FromArgb(190, 8, 9, 17));
                    args.Graphics.FillRectangle(overlay, ClientRectangle);
                };
            }
        }

        var brand = new Label
        {
            AutoSize = true,
            Text = "KYVORA",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(158, 123, 255),
            Location = new Point(30, 25)
        };
        var title = new Label
        {
            AutoSize = true,
            Text = "Instalando KyvoraClient",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            Location = new Point(28, 67)
        };
        _status = new Label
        {
            AutoSize = true,
            Text = "Preparando la instalación...",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(93, 225, 193),
            Location = new Point(32, 139)
        };
        _detail = new Label
        {
            AutoSize = false,
            Text = "Copiando los archivos del cliente a tu perfil de Windows.",
            ForeColor = Color.FromArgb(157, 168, 190),
            Location = new Point(32, 166),
            Size = new Size(490, 39)
        };
        _progress = new ProgressBar
        {
            Location = new Point(32, 216),
            Size = new Size(490, 9),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 18
        };
        _finish = new Button
        {
            Text = "Abrir KyvoraClient",
            Location = new Point(365, 260),
            Size = new Size(157, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(158, 123, 255),
            ForeColor = Color.FromArgb(19, 16, 30),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Enabled = false
        };
        _finish.FlatAppearance.BorderSize = 0;
        _finish.Click += (_, _) => OpenClient();

        Controls.AddRange([brand, title, _status, _detail, _progress, _finish]);
        Shown += async (_, _) => await InstallAsync();
    }

    private async Task InstallAsync()
    {
        try
        {
            SetState("Extrayendo KyvoraClient...", "Instalando en tu perfil, sin permisos de administrador.", false);
            Directory.CreateDirectory(_installDirectory);
            var assembly = Assembly.GetExecutingAssembly();
            await using var payload = assembly.GetManifestResourceStream("KyvoraClientPayload.zip")
                ?? throw new InvalidOperationException("El instalador no incluye los archivos de KyvoraClient.");
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var destination = Path.GetFullPath(Path.Combine(_installDirectory, entry.FullName));
                var installRoot = Path.GetFullPath(_installDirectory) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El paquete contiene una ruta de instalación no válida.");

                if (string.Equals(entry.Name, "appsettings.json", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(destination))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = entry.Open();
                await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output);
            }

            CreateShortcuts();
            SetState("¡KyvoraClient está instalado!", "Ya aparece en el menú Inicio y en tu escritorio.", true);
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 100;
            _finish.Enabled = true;
        }
        catch (Exception exception)
        {
            SetState(
                "No se pudo completar la instalación",
                $"{exception.Message} Si persiste, vuelve a ejecutar el instalador.",
                false);
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = 0;
            MessageBox.Show(
                this,
                exception.Message,
                "Error al instalar KyvoraClient",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CreateShortcuts()
    {
        var executable = Path.Combine(_installDirectory, "KyvoraClient.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("No se encontró KyvoraClient.exe en el paquete.", executable);

        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "KyvoraClient");
        Directory.CreateDirectory(startMenuDirectory);
        CreateShortcut(Path.Combine(desktopDirectory, "KyvoraClient.lnk"), executable);
        CreateShortcut(Path.Combine(startMenuDirectory, "KyvoraClient.lnk"), executable);
    }

    private static void CreateShortcut(string path, string executable)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows no pudo crear el acceso directo.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Windows no pudo iniciar el creador de accesos directos.");
            dynamic shellObject = shell;
            shortcut = shellObject.CreateShortcut(path);
            dynamic shortcutObject = shortcut;
            shortcutObject.TargetPath = executable;
            shortcutObject.WorkingDirectory = Path.GetDirectoryName(executable);
            shortcutObject.Description = "Launcher de Minecraft Java 1.8.9";
            shortcutObject.IconLocation = executable + ",0";
            shortcutObject.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private void OpenClient()
    {
        var executable = Path.Combine(_installDirectory, "KyvoraClient.exe");
        try
        {
            Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = _installDirectory,
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "No se pudo abrir KyvoraClient", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetState(string title, string detail, bool completed)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetState(title, detail, completed));
            return;
        }

        _status.Text = title;
        _status.ForeColor = completed ? Color.FromArgb(93, 225, 193) : Color.FromArgb(231, 185, 92);
        _detail.Text = detail;
    }
}
