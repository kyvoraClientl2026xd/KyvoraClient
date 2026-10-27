using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyvoraPvp;

public partial class App : System.Windows.Application
{
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var scale = new ScaleTransform(1, 1);
        button.RenderTransformOrigin = new Point(0.5, 0.5);
        button.RenderTransform = scale;
        var clickAnimation = new DoubleAnimation(1, 0.95, new Duration(TimeSpan.FromMilliseconds(85)))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, clickAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, clickAnimation);
    }
}
