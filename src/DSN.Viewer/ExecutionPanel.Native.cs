// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using DSN.Core;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed record NativeExportOptions(string Tools, string Directory, string Mode);

internal sealed partial class ExecutionPanel
{
    private bool nativeBusy;
    private async void ExportNative()
    {
        Pause(); if (Session is null || nativeBusy) return;
        var architecture = Session.Architecture; var tools = "";
        var settingsPath = Path.Combine(assetsRoot, "settings", "native-tools.json");
        try
        {
            if (File.Exists(settingsPath) && new FileInfo(settingsPath).Length <= 65536)
                tools = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settingsPath))?.GetValueOrDefault(architecture) ?? "";
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { }
        var dialog = new NativeOptionsWindow(architecture, tools, Path.Combine(assetsRoot, "results", NativeOptionsWindow.FolderName(architecture))) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Options is not { } options) return;
        nativeBusy = true; Refresh();
        UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Native.Building", architecture)); diagnostic.Foreground = Brushes.DarkSlateGray;
        try
        {
            var file = await SaveNativeTo(options);
            if (options.Tools.Length > 0)
            {
                var saved = new Dictionary<string, string>();
                try { if (File.Exists(settingsPath)) saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settingsPath)) ?? []; }
                catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { }
                saved[architecture] = options.Tools;
                try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath, JsonSerializer.Serialize(saved, DSNProgramBinder.JsonOptions)); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
            UILocalization.Set(diagnostic, "Text", () => DSNText.Russian(options.Mode == "source" ? "Native.SourceReady" : "Native.Complete", file));
            diagnostic.Foreground = Brushes.DarkSlateGray;
        }
        catch (Exception error) { UILocalization.Set(diagnostic, "Text", () => DSNText.Translate(error.Message)); diagnostic.Foreground = Brushes.DarkRed; }
        finally { nativeBusy = false; Refresh(); }
    }

    internal async Task<string> SaveNativeTo(NativeExportOptions options)
    {
        Pause(); if (Session is null || document is null) throw new InvalidOperationException(DSNText.Russian("Report.NoSession"));
        var session = Session; var hash = document.SourceSha256;
        if (options.Mode is not ("source" or "object" or "image")) throw new ArgumentException(DSNText.Russian("Native.ModeChoice"));
        if (Directory.Exists(options.Directory) && Directory.EnumerateFileSystemEntries(options.Directory).Any()) throw new ArgumentException(DSNText.Russian("Native.EmptyFolder"));
        var source = NativeAssembly.Generate(session.Bound.Program, hash);
        if (options.Mode == "source") { NativeAssembly.Write(source, options.Directory); return options.Directory; }
        var result = await NativeBuild.BuildAsync(source, NativeToolPaths.ForArchitecture(session.Architecture, options.Tools), options.Directory, linkImage: options.Mode == "image");
        return result.Image ?? result.Object;
    }
}

internal sealed class NativeOptionsWindow : Window
{
    internal NativeExportOptions? Options { get; private set; }
    internal TextBox Tools { get; } = new();
    internal TextBox Output { get; } = new();
    internal ComboBox Mode { get; } = new();
    internal static string FolderName(string architecture) => "native-" + architecture + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
    internal NativeOptionsWindow(string architecture, string tools, string output)
    {
        NameScope.SetNameScope(this, new NameScope());
        UILocalization.Set(this, "Title", () => DSNText.Russian("Native.Title", architecture));
        Width = 640; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = Branding.Icon;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = panel;
        void Text(string key) => panel.Children.Add(UILocalization.With(new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 5) }, ("Text", () => DSNText.Russian(key))));
        Text("Native.ContextHint"); Text("Native.ModeChoice");
        foreach (var key in new[] { "Native.Mode.Source", "Native.Mode.Object", "Native.Mode.Image" }) Mode.Items.Add(UILocalization.Choice(DSNText.Russian(key)));
        Mode.SelectedIndex = architecture == "ADSP21" ? 0 : 2; Mode.Name = "NativeMode"; RegisterName(Mode.Name, Mode); panel.Children.Add(Mode);
        Text(architecture == "ADSP21" ? "Native.ToolsHintADSP" : "Native.ToolsHintGNU");
        var toolsRow = new DockPanel(); panel.Children.Add(toolsRow);
        var chooseTools = UILocalization.With(new Button { Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) }, ("Content", () => DSNText.Russian("Native.Browse")));
        DockPanel.SetDock(chooseTools, Dock.Right); toolsRow.Children.Add(chooseTools);
        Tools.Text = tools; Tools.Name = "NativeTools"; RegisterName(Tools.Name, Tools); toolsRow.Children.Add(Tools);
        Text("Native.Output");
        var outputRow = new DockPanel(); panel.Children.Add(outputRow);
        var chooseOutput = UILocalization.With(new Button { Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) }, ("Content", () => DSNText.Russian("Native.Browse")));
        DockPanel.SetDock(chooseOutput, Dock.Right); outputRow.Children.Add(chooseOutput);
        Output.Text = output; Output.Name = "NativeOutput"; RegisterName(Output.Name, Output); outputRow.Children.Add(Output);
        if (architecture == "ADSP21") Text("Native.LicenseHint");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkRed, Margin = new Thickness(0, 8, 0, 8) }; panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(buttons);
        var build = UILocalization.With(new Button { IsDefault = true, Padding = new Thickness(16, 6, 16, 6), Name = "NativeCreate" }, ("Content", () => DSNText.Russian("Native.Create")));
        RegisterName(build.Name, build); buttons.Children.Add(build);
        buttons.Children.Add(UILocalization.With(new Button { IsCancel = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(10, 0, 0, 0) }, ("Content", () => DSNText.Russian("Report.Cancel"))));
        Mode.SelectionChanged += (_, _) => { Tools.IsEnabled = chooseTools.IsEnabled = Mode.SelectedIndex != 0; };
        Tools.IsEnabled = chooseTools.IsEnabled = Mode.SelectedIndex != 0;
        chooseTools.Click += (_, _) => { var picker = new OpenFolderDialog { Title = DSNText.Translate(DSNText.Russian("Native.ToolFolder")) }; if (picker.ShowDialog(this) == true) Tools.Text = picker.FolderName; };
        chooseOutput.Click += (_, _) => { var picker = new OpenFolderDialog { Title = DSNText.Translate(DSNText.Russian("Native.OutputParent")) }; if (picker.ShowDialog(this) == true) Output.Text = Path.Combine(picker.FolderName, FolderName(architecture)); };
        build.Click += (_, _) =>
        {
            try
            {
                var mode = Mode.SelectedIndex switch { 0 => "source", 1 => "object", 2 => "image", _ => throw new ArgumentException(DSNText.Russian("Native.ModeChoice")) };
                var directory = Path.GetFullPath(Output.Text.Trim());
                if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new ArgumentException(DSNText.Russian("Native.EmptyFolder"));
                if (mode != "source")
                {
                    var paths = NativeToolPaths.ForArchitecture(architecture, Tools.Text.Trim());
                    foreach (var tool in new[] { paths.Assembler, paths.Inspector, paths.Disassembler }.Concat(mode == "image" ? [paths.Linker] : []))
                        if (!File.Exists(tool)) throw new ArgumentException(DSNText.Russian("Native.Tool", tool));
                }
                Options = new(Tools.Text.Trim(), directory, mode); DialogResult = true;
            }
            catch (Exception issue) { UILocalization.Set(error, "Text", () => DSNText.Translate(issue.Message)); }
        };
    }
}
