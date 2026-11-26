// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DSN.Viewer;

internal sealed class AboutWindow : Window
{
    internal AboutWindow()
    {
        UILocalization.Set(this, "Title", () => DSNText.Russian("Viewer.AboutWindow.Text001")); Icon = Branding.Icon;
        Width = 560; Height = 440; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        Background = new SolidColorBrush(Color.FromRgb(241, 244, 247));
        var root = new DockPanel { Margin = new Thickness(26), Background = Background }; Content = root;
        var close = UILocalization.With<global::System.Windows.Controls.Button>(new Button { Content = DSNText.Russian("Viewer.AboutWindow.Text002"), IsCancel = true, IsDefault = true, Padding = new Thickness(24, 7, 24, 7), HorizontalAlignment = HorizontalAlignment.Right }, ("Content", () => (object?)(DSNText.Russian("Viewer.AboutWindow.Text002"))));
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        var text = new StackPanel(); root.Children.Add(text);
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        heading.Children.Add(Branding.LogoTile(76));
        var title = new StackPanel { Margin = new Thickness(18, 4, 0, 0) };
        title.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = "AARIS · DSN Compiler", FontSize = 23, FontWeight = FontWeights.SemiBold }, ("Text", () => (object?)("AARIS · DSN Compiler"))));
        var version = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AboutWindow).Assembly.GetName().Version?.ToString() ?? DSNText.Russian("Viewer.AboutWindow.Text003");
        title.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Concat(DSNText.Russian("Viewer.AboutWindow.Text004"), version.Split('+')[0]), Margin = new Thickness(0, 7, 0, 0), Foreground = Brushes.DimGray }, ("Text", () => (object?)(DSNText.Concat(DSNText.Russian("Viewer.AboutWindow.Text004"), version.Split('+')[0])))));
        heading.Children.Add(title); text.Children.Add(heading);
        Add(DSNText.Russian("Viewer.AboutWindow.Text005"));
        Add(DSNText.Russian("Viewer.AboutWindow.Text006"));
        Add(DSNText.Russian("Viewer.AboutWindow.Text007"));
        Add(DSNText.Russian("Viewer.AboutWindow.Text008"));

        void Add(string value) => text.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 13) }, ("Text", () => (object?)(value))));
    }
}
