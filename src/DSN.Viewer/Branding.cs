// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DSN.Viewer;

internal static class Branding
{
    private static readonly ResourceDictionary Resources = new()
    {
        Source = new Uri("/DSNViewer;component/Assets/Branding.xaml", UriKind.Relative)
    };

    internal static DrawingImage Logo { get; } = LoadLogo();
    internal static BitmapFrame Icon { get; } = BitmapFrame.Create(new Uri("pack://application:,,,/DSNViewer;component/Assets/AARIS.ico"));

    private static DrawingImage LoadLogo()
    {
        var image = (DrawingImage)Resources["AARISLogo"];
        image.Freeze();
        return image;
    }

    internal static Border LogoTile(double size) => UILocalization.With<global::System.Windows.Controls.Border>(new()
    {
        Width = size, Height = size, Background = Brushes.White, CornerRadius = new CornerRadius(size / 8),
        Padding = new Thickness(size / 20), ToolTip = "AARIS",
        Child = new Image { Source = Logo, Stretch = Stretch.Uniform }
    }, ("ToolTip", () => (object?)("AARIS")));
}
