// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using DSN.Core;
using DSN.Execution;
using Page = DSN.Core.Page;

namespace DSN.Viewer;

public sealed class MainWindow : Window
{
    internal LogicProgramCanvas Canvas { get; } = new();
    internal ExecutionPanel Execution { get; }
    internal ComboBox LanguageChoice { get; } = new() { Width = 70, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new();
    private readonly TextBlock fileName = new();
    private readonly TextBlock zoom = new();
    private readonly ComboBox pages = new() { MinWidth = 140, Margin = new Thickness(6, 0, 18, 0), DisplayMemberPath = nameof(Page.Name) };
    private LogicProgramDocument? document;
    private ConnectionGraph? connections;
    private string? currentFile;
    private Instance? selected;

    public MainWindow(string? initialFile, string? assetsRoot = null, bool controlProfiles = false)
    {
        Execution = new(assetsRoot ?? AppContext.BaseDirectory, controlProfiles);
        UILocalization.Set(this, "Title", () => DSNText.Russian("Viewer.MainWindow.Text001")); Width = 1560; Height = 960; MinWidth = 1160; MinHeight = 760;
        Icon = Branding.Icon;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(241, 244, 247));
        var root = new DockPanel { Background = Background }; Content = root;
        var header = new Border { Background = new SolidColorBrush(Color.FromRgb(23, 37, 51)), Padding = new Thickness(20, 10, 20, 10) };
        var heading = new DockPanel();
        var logo = Branding.LogoTile(48); logo.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(logo, Dock.Left); heading.Children.Add(logo);
        var about = UILocalization.With<global::System.Windows.Controls.Button>(new Button { Content = DSNText.Russian("Viewer.MainWindow.Text002"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }, ("Content", () => (object?)(DSNText.Russian("Viewer.MainWindow.Text002"))));
        about.Click += (_, _) => { Execution.Pause(); new AboutWindow { Owner = this }.ShowDialog(); };
        DockPanel.SetDock(about, Dock.Right); heading.Children.Add(about);
        var languages = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        languages.Children.Add(UILocalization.With(new TextBlock { Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center },
            ("Text", () => DSNText.Russian("Localization.Language"))));
        LanguageChoice.ItemsSource = new[] { "RU", "EN" }; LanguageChoice.SelectedItem = DSNText.Language.ToUpperInvariant();
        LanguageChoice.SelectionChanged += (_, _) =>
        {
            if (LanguageChoice.SelectedItem is string language)
            {
                UILocalization.SelectLanguage(language.ToLowerInvariant());
                UILocalization.Set(LanguageChoice, "ToolTip", () => UILocalization.SettingsDiagnostic ?? DSNText.CatalogDiagnostic ?? DSNText.Russian("Localization.Language"));
            }
        };
        UILocalization.Observe(this, () => LanguageChoice.SelectedItem = DSNText.Language.ToUpperInvariant());
        languages.Children.Add(LanguageChoice); DockPanel.SetDock(languages, Dock.Right); heading.Children.Add(languages);
        UILocalization.Set(LanguageChoice, "ToolTip", () => DSNText.CatalogDiagnostic ?? DSNText.Russian("Localization.Language"));
        var title = new StackPanel();
        title.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.MainWindow.Text003"), FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }, ("Text", () => (object?)(DSNText.Russian("Viewer.MainWindow.Text003")))));
        fileName.Foreground = new SolidColorBrush(Color.FromRgb(184, 202, 218)); fileName.Margin = new Thickness(0, 4, 0, 0); title.Children.Add(fileName);
        heading.Children.Add(title); header.Child = heading; DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var toolbar = new WrapPanel { Margin = new Thickness(14, 8, 14, 8) };
        AddButton(toolbar, DSNText.Russian("Viewer.MainWindow.Text004"), Open); toolbar.Children.Add(pages);
        AddButton(toolbar, DSNText.Russian("Viewer.MainWindow.Text005"), () => Canvas.Fit(false)); AddButton(toolbar, DSNText.Russian("Viewer.MainWindow.Text006"), () => Canvas.Fit(true));
        AddButton(toolbar, DSNText.Russian("Viewer.MainWindow.Text007"), () => { if (selected is not null) Canvas.FocusInstance(selected); });
        AddButton(toolbar, "−", () => Canvas.Zoom(1 / 1.25)); AddButton(toolbar, "+", () => Canvas.Zoom(1.25));
        zoom.VerticalAlignment = VerticalAlignment.Center; zoom.Margin = new Thickness(10, 0, 18, 0); toolbar.Children.Add(zoom);
        var grid = UILocalization.With<global::System.Windows.Controls.CheckBox>(new CheckBox { Content = DSNText.Russian("Viewer.MainWindow.Text008"), VerticalAlignment = VerticalAlignment.Center }, ("Content", () => (object?)(DSNText.Russian("Viewer.MainWindow.Text008"))));
        grid.Checked += (_, _) => { Canvas.ShowGrid = true; Canvas.InvalidateVisual(); }; grid.Unchecked += (_, _) => { Canvas.ShowGrid = false; Canvas.InvalidateVisual(); };
        toolbar.Children.Add(grid); DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        DockPanel.SetDock(Execution.Controls, Dock.Top); root.Children.Add(Execution.Controls);
        DockPanel.SetDock(Execution.ProfileNotice, Dock.Top); root.Children.Add(Execution.ProfileNotice);
        var footer = new Border { Padding = new Thickness(14, 8, 14, 8), Background = Brushes.White, Child = status };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new Grid { Margin = new Thickness(14, 8, 14, 10) };
        body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430), MinWidth = 350 });
        var left = new Grid(); left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 150 });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 230 });
        var paper = new Border { Background = Brushes.White, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Child = Canvas }; left.Children.Add(paper);
        var splitter = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetRow(splitter, 1); left.Children.Add(splitter);
        var history = new DockPanel();
        var historyTitle = UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.MainWindow.Text009"), TextWrapping = TextWrapping.Wrap, Padding = new Thickness(6) }, ("Text", () => (object?)(DSNText.Russian("Viewer.MainWindow.Text009"))));
        DockPanel.SetDock(historyTitle, Dock.Top); history.Children.Add(historyTitle); history.Children.Add(Execution.HistoryView);
        Grid.SetRow(history, 2); left.Children.Add(history); body.Children.Add(left);
        var vertical = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(vertical, 1); body.Children.Add(vertical);
        Grid.SetColumn(Execution, 2); body.Children.Add(Execution); root.Children.Add(body);
        Canvas.ZoomChanged += value => UILocalization.Set(zoom, "Text", () => DSNText.Compose($"{value * 100:0}%"));
        Canvas.SelectionChanged += instance => { selected = instance; Execution.SelectBlock(instance?.Reference); ShowDetails(); };
        Execution.RestoreSelection += reference => Canvas.Select(document?.Pages.SelectMany(p => p.Instances).FirstOrDefault(i => i.Reference == reference));
        Execution.Parts.SelectionChanged += (_, _) => { if (Execution.Parts.SelectedItem is ListBoxItem { Tag: Instance instance }) { Canvas.Select(instance); Canvas.FocusInstance(instance); } };
        Execution.StateChanged += () =>
        {
            var visibility = Execution.Session is null ? Visibility.Collapsed : Visibility.Visible;
            if (history.Visibility != visibility)
            {
                history.Visibility = splitter.Visibility = visibility;
                left.RowDefinitions[2].MinHeight = Execution.Session is null ? 0 : 230;
                left.RowDefinitions[2].Height = Execution.Session is null ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
                left.RowDefinitions[1].Height = new GridLength(Execution.Session is null ? 0 : 6);
            }
            Canvas.SetExecution(Execution.Session); ShowDetails();
        };
        pages.SelectionChanged += (_, _) => { if (document is not null && pages.SelectedItem is Page page) ShowPage(page); };
        Closed += (_, _) => Execution.Pause();
        if (initialFile is not null) LoadFile(initialFile);
        else { UILocalization.Set(fileName, "Text", () => DSNText.Russian("Viewer.MainWindow.Text010")); UILocalization.Set(Execution.BlockDetails, "Text", () => DSNText.Russian("Viewer.MainWindow.Text011")); }
    }

    private static void AddButton(Panel panel, string text, Action action)
    {
        var button = UILocalization.With<global::System.Windows.Controls.Button>(new Button { Content = text, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0) }, ("Content", () => (object?)(text)));
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private void Open()
    {
        Execution.Pause();
        var dialog = UILocalization.With<global::Microsoft.Win32.OpenFileDialog>(new OpenFileDialog { Filter = DSNText.Russian("Viewer.MainWindow.Text012"), CheckFileExists = true,
            AddToRecent = false, InitialDirectory = currentFile is null ? AppContext.BaseDirectory : Path.GetDirectoryName(currentFile) }, ("Filter", () => (object?)(DSNText.Russian("Viewer.MainWindow.Text012"))));
        if (dialog.ShowDialog(this) == true) LoadFile(dialog.FileName);
    }
    internal void LoadFile(string path)
    {
        Execution.Pause();
        try
        {
            var loaded = DSNReader.Read(path);
            foreach (var page in loaded.Pages) ConnectionGraph.Build(loaded, page);
            document = loaded; currentFile = Path.GetFullPath(path);
            UILocalization.Set(fileName, "Text", () => DSNText.Concat(DSNText.Concat(DSNText.Concat(Path.GetFileName(path), "  ·  OrCAD Capture "), document.FormatVersion), DSNText.Russian("Viewer.MainWindow.Text013")));
            pages.ItemsSource = document.Pages; pages.SelectedIndex = 0;
            Execution.Bind(document); UILocalization.Set(this, "Title", () => DSNText.Concat("DSN Compiler – ", Path.GetFileName(path)));
        }
        catch (Exception error) { MessageBox.Show(this, DSNText.Translate(error.Message), DSNText.Translate(DSNText.Russian("Viewer.MainWindow.Text014")), MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ShowPage(Page page)
    {
        if (document is null) return;
        selected = null; connections = ConnectionGraph.Build(document, page);
        Canvas.SetDocument(document, page); Execution.Parts.Items.Clear();
        foreach (var instance in page.Instances.OrderBy(i => i.Reference, StringComparer.Ordinal))
            Execution.Parts.Items.Add(UILocalization.With<global::System.Windows.Controls.ListBoxItem>(new ListBoxItem { Content = DSNText.Concat(DSNText.Concat(instance.Reference, "   "), instance.Value), Tag = instance, Padding = new Thickness(5) }, ("Content", () => (object?)(DSNText.Concat(DSNText.Concat(instance.Reference, "   "), instance.Value)))));
        UILocalization.Set(status, "Text", () => DSNText.Russian("Viewer.MainWindow.Text015", page.Instances.Count, page.Links.Count, document.Symbols.Count));
        ShowDetails();
    }
    private void ShowDetails()
    {
        if (selected is null || document is null) { UILocalization.Set(Execution.BlockDetails, "Text", () => DSNText.Russian("Viewer.MainWindow.Text016")); return; }
        var instance = selected; var session = Execution.Session;
        var symbol = document.Symbol(instance);
        var text = DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(instance.Reference, " · "), instance.Value), DSNText.Russian("Viewer.MainWindow.Text017")), DSNText.Join("\n", instance.Ports.Select(p =>
        {
            var name = symbol.Ports[p.Number - 1].Name; var key = DSNText.Concat(DSNText.Concat(instance.Reference, "."), name);
            var names = connections is not null && connections.Ports.TryGetValue(key, out var connection) ? DSNText.Join(", ", connection.Names) : "";
            var value = session is not null && session.Bound.Ports.TryGetValue(key, out var valueBinding) ? DSNText.Concat(DSNText.Concat(" = ", ExecutionSession.FormatWord(session.ReadValue(valueBinding))), (valueBinding.Constant is null ? "" : DSNText.Russian("Viewer.MainWindow.Text018"))) : "";
            return DSNText.Concat(DSNText.Concat(name, value), (names.Length > 0 ? DSNText.Concat("\n  ", names) : ""));
        })));
        if (session is not null)
        {
            var locals = session.Variables.Where(n => n.StartsWith(DSNText.Concat(instance.Reference, "::"), StringComparison.OrdinalIgnoreCase)).ToArray();
            text += DSNText.Concat(DSNText.Russian("Viewer.MainWindow.Text019"), (locals.Length == 0 ? DSNText.Russian("Viewer.MainWindow.Text020") : DSNText.Join("\n", locals.Select(n => DSNText.Concat(DSNText.Concat(n, " = "), ExecutionSession.FormatWord(session.ReadWord(n)))))));
            var block = session.LastStep?.Blocks.FirstOrDefault(b => b.Instance == instance.Reference);
            if (block is not null)
            {
                var changes = block.Changes.Where(c => c.Before != c.After).Select(c =>
                {
                    var name = session.Variables.FirstOrDefault(n => session.Bound.Program.Addresses[n] == session.Bound.Program.Addresses[c.Name]) ?? c.Name;
                    return DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(name, ": "), ExecutionSession.FormatWord(c.Before)), " → "), ExecutionSession.FormatWord(c.After));
                }).ToArray();
                text += DSNText.Concat(DSNText.Russian("Viewer.MainWindow.Text021"), (changes.Length == 0 ? DSNText.Russian("Viewer.MainWindow.Text022") : DSNText.Join("\n", changes)));
            }
            var source = session.Bound.Program.SourceForInstance(instance.Reference);
            text += DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.MainWindow.Text023"), Path.GetFileName(source.File)), ":"), source.Line), DSNText.Russian("Viewer.MainWindow.Text024")), session.LibrarySha256);
        }
        else if (Execution.Readiness?.Functions.FirstOrDefault(f => f.Function == instance.Value) is { } readiness)
            text += DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.MainWindow.Text025"), readiness.Diagnostic), DSNText.Russian("Viewer.MainWindow.Text026"));
        UILocalization.Set(Execution.BlockDetails, "Text", () => text);
    }
}
