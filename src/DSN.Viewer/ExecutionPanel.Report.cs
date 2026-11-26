// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DSN.Core;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed partial class ExecutionPanel
{
    private string? lastReport;
    private void ExportReport()
    {
        Pause(); if (Session is null) return;
        var dialog = new ReportOptionsWindow(checked((int)Session.Samples[^1].Tick)) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var files = SaveReportTo(Path.Combine(assetsRoot, "results"), dialog.Options!); lastReport = files.Html;
            UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Report.Saved", files.Html)); diagnostic.Foreground = Brushes.DarkSlateGray;
            ((Button)FindName("OpenReport")).IsEnabled = true;
        }
        catch (Exception error) { UILocalization.Set(diagnostic, "Text", () => DSNText.Translate(error.Message)); diagnostic.Foreground = Brushes.DarkRed; }
    }
    internal ReportFiles SaveReportTo(string directory, ReportOptions options)
    {
        Pause(); if (Session is null || document is null) throw new InvalidOperationException(DSNText.Russian("Report.NoSession"));
        var snapshot = Session.CreateReport(options, watchRows.Where(r => r.Watch).Select(r => r.Name));
        var svg = options.Scheme ? LogicProgramSvg.Create(document) : "";
        return ExecutionReport.Save(directory, snapshot, svg, options.Json);
    }
    private void OpenReport()
    {
        if (lastReport is null || !File.Exists(lastReport)) return;
        try { Process.Start(new ProcessStartInfo(lastReport) { UseShellExecute = true }); }
        catch (Exception error) { UILocalization.Set(diagnostic, "Text", () => DSNText.Translate(error.Message)); diagnostic.Foreground = Brushes.DarkRed; }
    }
}

internal sealed class ReportOptionsWindow : Window
{
    internal ReportOptions? Options { get; private set; }
    internal ReportOptionsWindow(int last)
    {
        Title = DSNText.Translate(DSNText.Russian("Report.Title")); Width = 490; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize; Icon = Branding.Icon;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = panel;
        void Text(string key) => panel.Children.Add(new TextBlock { Text = DSNText.Translate(DSNText.Russian(key)), Margin = new Thickness(0, 7, 0, 5), TextWrapping = TextWrapping.Wrap });
        Text("Report.RangeChoice");
        var from = new TextBox { Text = "0", Name = "FirstTick" }; var to = new TextBox { Text = last.ToString(System.Globalization.CultureInfo.InvariantCulture), Name = "LastTick" };
        Text("Report.FirstTick"); panel.Children.Add(from); Text("Report.LastTick"); panel.Children.Add(to);
        CheckBox Choice(string key, bool value) { var box = new CheckBox { Content = DSNText.Translate(DSNText.Russian(key)), IsChecked = value, Margin = new Thickness(0, 7, 0, 0) }; panel.Children.Add(box); return box; }
        var registers = Choice("Report.IncludeRegisters", true); var scheme = Choice("Report.IncludeScheme", true);
        var diagrams = Choice("Report.IncludeDiagrams", true); var json = Choice("Report.IncludeJson", true);
        var language = new ComboBox { ItemsSource = new[] { "RU", "EN" }, SelectedIndex = DSNText.Language == "en" ? 1 : 0, Margin = new Thickness(0, 10, 0, 0) }; panel.Children.Add(language);
        var error = new TextBlock { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) }; panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(buttons);
        var save = new Button { Content = DSNText.Translate(DSNText.Russian("Report.Create")), IsDefault = true, Padding = new Thickness(15, 6, 15, 6) }; buttons.Children.Add(save);
        var cancel = new Button { Content = DSNText.Translate(DSNText.Russian("Report.Cancel")), IsCancel = true, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(15, 6, 15, 6) }; buttons.Children.Add(cancel);
        save.Click += (_, _) =>
        {
            if (!int.TryParse(from.Text, out var first) || !int.TryParse(to.Text, out var end) || first < 0 || end < first || end > last)
            { error.Text = DSNText.Translate(DSNText.Russian("Report.InvalidRange")); return; }
            Options = new(first, end, registers.IsChecked == true, scheme.IsChecked == true, diagrams.IsChecked == true, json.IsChecked == true, language.SelectedIndex == 1 ? "en" : "ru");
            DialogResult = true;
        };
    }
}
