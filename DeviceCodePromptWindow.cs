using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Identity.Client;

namespace KyvoraPvp;

internal sealed class DeviceCodePromptWindow : Window
{
    public DeviceCodePromptWindow(DeviceCodeResult deviceCode)
    {
        Title = "Vincular cuenta Microsoft";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(18, 21, 34));
        Foreground = new SolidColorBrush(Color.FromRgb(245, 246, 250));
        FontFamily = new FontFamily("Segoe UI");
        ShowInTaskbar = false;
        Topmost = true;

        var layout = new StackPanel { Margin = new Thickness(24) };
        layout.Children.Add(new TextBlock
        {
            Text = "Confirma tu cuenta Microsoft",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        layout.Children.Add(new TextBlock
        {
            Text = "Microsoft te pedirá este código para conectar tu cuenta. Escríbelo solo en la página oficial y no lo compartas con nadie.",
            Foreground = new SolidColorBrush(Color.FromRgb(157, 168, 190)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var code = new TextBox
        {
            Text = deviceCode.UserCode,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(9),
            Background = new SolidColorBrush(Color.FromRgb(11, 13, 20)),
            Foreground = new SolidColorBrush(Color.FromRgb(213, 196, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(72, 60, 109)),
            BorderThickness = new Thickness(1)
        };
        layout.Children.Add(code);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        var copy = CreateButton("Copiar código", Color.FromRgb(36, 42, 60), Color.FromRgb(245, 246, 250));
        copy.Margin = new Thickness(0, 0, 8, 0);
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(deviceCode.UserCode);
            copy.Content = "¡Copiado!";
        };
        var open = CreateButton("Abrir Microsoft", Color.FromRgb(158, 123, 255), Color.FromRgb(19, 16, 30));
        open.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(deviceCode.VerificationUrl)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "No se pudo abrir Microsoft", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        actions.Children.Add(copy);
        actions.Children.Add(open);
        layout.Children.Add(actions);
        layout.Children.Add(new TextBlock
        {
            Text = "Deja el launcher abierto. Detectará la aprobación y conectará el perfil automáticamente.",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(127, 138, 163)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        });

        Content = layout;
    }

    private static Button CreateButton(string label, Color background, Color foreground)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(14, 9, 14, 9),
            Background = new SolidColorBrush(background),
            Foreground = new SolidColorBrush(foreground),
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        return button;
    }
}
