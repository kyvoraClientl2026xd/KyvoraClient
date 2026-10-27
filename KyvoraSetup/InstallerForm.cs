using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KyvoraSetup;

internal sealed class InstallerForm : Form
{
    private readonly Label _status;
    private readonly Label _detail;
    private readonly Label _progressText;
    private readonly Label _installPath;
    private readonly ProgressBar _progress;
    private readonly Button _finish;
    private bool _installationComplete;
    private readonly string _installDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "KyvoraClient");

    public InstallerForm()
    {
        Text = "Kyvora · Instalador";
        ClientSize = new Size(760, 500);
        MinimumSize = new Size(760, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(11, 13, 20);
        ForeColor = Color.FromArgb(245, 246, 250);
        Font = new Font("Segoe UI", 9F);
        DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
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

        var card = new Panel
        {
            Location = new Point(38, 36),
            Size = new Size(684, 420),
            BackColor = Color.FromArgb(246, 12, 15, 25)
        };
        card.Paint += (_, args) =>
        {
            using var border = new Pen(Color.FromArgb(76, 87, 108));
            args.Graphics.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
        };
        var brand = new Label
        {
            AutoSize = true,
            Text = "KYVORA  /  CLIENTE MINECRAFT",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 166, 255),
            Location = new Point(34, 27)
        };
        var logo = new Panel
        {
            Location = new Point(34, 68),
            Size = new Size(76, 76),
            BackColor = Color.FromArgb(45, 33, 74)
        };
        logo.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(Color.FromArgb(142, 103, 255));
            args.Graphics.FillEllipse(fill, 1, 1, 73, 73);
            using var letter = new Font("Segoe UI", 29F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            var size = args.Graphics.MeasureString("K", letter);
            args.Graphics.DrawString("K", letter, brush, (76 - size.Width) / 2, (76 - size.Height) / 2 - 2);
        };
        var title = new Label
        {
            AutoSize = true,
            Text = "Instalando KyvoraClient",
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            Location = new Point(132, 70)
        };
        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Tu cliente estará listo en unos momentos.",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(164, 174, 194),
            Location = new Point(134, 111)
        };
        _status = new Label
        {
            AutoSize = true,
            Text = "Preparando la instalación…",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(231, 185, 92),
            Location = new Point(36, 176)
        };
        _detail = new Label
        {
            AutoSize = false,
            Text = "Copiando los archivos del cliente a tu equipo.",
            ForeColor = Color.FromArgb(164, 174, 194),
            Location = new Point(36, 205),
            Size = new Size(608, 36)
        };
        _installPath = new Label
        {
            AutoSize = false,
            Text = _installDirectory,
            ForeColor = Color.FromArgb(129, 140, 161),
            Location = new Point(36, 256),
            Size = new Size(608, 22),
            AutoEllipsis = true
        };
        _progress = new ProgressBar
        {
            Location = new Point(36, 293),
            Size = new Size(608, 13),
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };
        _progressText = new Label
        {
            AutoSize = true,
            Text = "0%",
            ForeColor = Color.FromArgb(185, 166, 255),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(36, 315)
        };
        _finish = new Button
        {
            Text = "Abrir KyvoraClient",
            Location = new Point(448, 350),
            Size = new Size(196, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(158, 123, 255),
            ForeColor = Color.FromArgb(19, 16, 30),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Enabled = false
        };
        _finish.FlatAppearance.BorderSize = 0;
        _finish.Click += (_, _) => { if (_installationComplete) OpenClient(); else Close(); };
        var footer = new Label
        {
            AutoSize = true,
            Text = "Instalación en tu perfil de Windows  ·  No requiere permisos de administrador",
            ForeColor = Color.FromArgb(129, 140, 161),
            Location = new Point(36, 364)
        };

        card.Controls.AddRange([brand, logo, title, subtitle, _status, _detail, _installPath, _progress, _progressText, footer, _finish]);
        Controls.Add(card);
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
            var files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
            var totalBytes = files.Where(entry => !(string.Equals(entry.Name, "appsettings.json", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(_installDirectory, entry.FullName)))).Sum(entry => entry.Length);
            long copiedBytes = 0;
            var lastPercent = -1;
            var buffer = new byte[1024 * 1024];
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
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    copiedBytes += read;
                    var percent = totalBytes <= 0 ? 100 : (int)Math.Min(99, copiedBytes * 100d / totalBytes);
                    if (percent != lastPercent)
                    {
                        _progress.Value = percent;
                        _progressText.Text = $"{percent}%  ·  {copiedBytes / 1024d / 1024d:0} de {totalBytes / 1024d / 1024d:0} MB";
                        lastPercent = percent;
                    }
                }
            }

            CreateShortcuts();
            SetState("¡KyvoraClient está instalado!", "Ya aparece en el menú Inicio y en tu escritorio.", true);
            _progress.Value = 100;
            _progressText.Text = "100%  ·  Instalación completa";
            _installationComplete = true;
            _finish.Enabled = true;
        }
        catch (Exception exception)
        {
            SetState(
                "No se pudo completar la instalación",
                $"{exception.Message} Si persiste, vuelve a ejecutar el instalador.",
                false);
            _progress.Value = 0;
            _progressText.Text = "Instalación incompleta";
            _finish.Text = "Cerrar instalador";
            _finish.Enabled = true;
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
        if (title.StartsWith("No se pudo", StringComparison.Ordinal))
            _status.ForeColor = Color.FromArgb(255, 117, 133);
    }
}
