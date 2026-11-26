// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using DSN.Core;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed class InitialWordRow
{
    public bool Override { get; set; }
    public string Name { get; init; } = "";
    public string Value { get; set; } = "0";
}
internal sealed class WatchRow
{
    public bool Watch { get; set; }
    public string Name { get; init; } = "";
    public string Value { get; set; } = "";
    public string Changed { get; set; } = "";
}

internal sealed partial class ExecutionPanel : DockPanel
{
    private readonly string assetsRoot;
    private LogicProgramDocument? document;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly TextBlock counter = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly TextBlock diagnostic = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkRed, Margin = new Thickness(0, 6, 0, 6) };
    private readonly TextBlock inputHint = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly ComboBox inputNames = new() { MinWidth = 150, Margin = new Thickness(0, 8, 0, 8) };
    private readonly ComboBox profileChoice = new() { Width = 235, Margin = new Thickness(8, 0, 8, 3), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox implementationChoice = new() { Width = 115, Margin = new Thickness(0, 0, 8, 3), VerticalAlignment = VerticalAlignment.Center };
    private string activeImplementation = "";
    private bool updatingImplementation;
    private string? activeProfile;
    private readonly bool controlProfiles;
    private readonly (string File, ExecutionProfile Profile)[] availableProfiles;
    private bool genericMode;
    private bool updatingProfile;
    private readonly TextBox inputValue = UILocalization.With<global::System.Windows.Controls.TextBox>(new() { MinWidth = 130, Padding = new Thickness(5), Text = "0" }, ("Text", () => (object?)("0")));
    private readonly CheckBox scenario = UILocalization.With<global::System.Windows.Controls.CheckBox>(new() { Content = DSNText.Russian("Viewer.ExecutionPanel.Text001"), Margin = new Thickness(0, 6, 0, 8) }, ("Content", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text001"))));
    private readonly CheckBox[] bits = new CheckBox[16];
    private readonly ObservableCollection<InitialWordRow> initialRows = [];
    private readonly ObservableCollection<WatchRow> watchRows = [];
    private readonly DataGrid initialGrid;
    private readonly DataGrid watchGrid;
    private readonly DataGrid readinessGrid = Table();
    private readonly Button play;
    private readonly List<Button> executionButtons = [];
    private readonly TabControl tabs = new();
    private readonly DataTable historyTable = new();
    private bool updatingBits;
    private string[] historyNames = [];
    public ExecutionSession? Session { get; private set; }
    public ProgramReadiness? Readiness { get; private set; }
    public event Action? StateChanged;
    public WrapPanel Controls { get; } = new() { Margin = new Thickness(14, 0, 14, 6) };
    public TextBlock ProfileNotice { get; } = new() { TextWrapping = TextWrapping.Wrap, Padding = new Thickness(12, 8, 12, 8), Background = new SolidColorBrush(Color.FromRgb(255, 244, 210)) };
    public DataGrid StateHistory { get; } = Table();
    public TextBlock BlockDetails { get; } = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
    public ListBox Parts { get; } = new() { MaxHeight = 190, Margin = new Thickness(0, 0, 0, 10) };
    public string Diagnostic => diagnostic.Text;
    public bool IsRunning => timer.IsEnabled;

    public ExecutionPanel(string root, bool includeControlProfiles = false)
    {
        NameScope.SetNameScope(this, new NameScope());
        assetsRoot = root;
        controlProfiles = includeControlProfiles; genericMode = !controlProfiles;
        availableProfiles = includeControlProfiles && Directory.Exists(Path.Combine(root, "profiles"))
            ? Directory.GetFiles(Path.Combine(root, "profiles"), "*.validation.json")
                .Select(p => (File: Path.GetFileName(p), Profile: DSNProgramBinder.ReadJson<ExecutionProfile>(p)))
                .OrderBy(p => p.Profile.Order.Length).ThenBy(p => p.Profile.Id, StringComparer.Ordinal).ToArray() : [];
        NameControl(inputNames, "InputName"); NameControl(inputValue, "InputValue"); NameControl(scenario, "ScenarioMode");
        Button(Controls, DSNText.Russian("Viewer.ExecutionPanel.Text002"), "Reset", Reset);
        Button(Controls, DSNText.Russian("Viewer.ExecutionPanel.Text003"), "Step", () => Advance(1));
        Button(Controls, DSNText.Russian("Viewer.ExecutionPanel.Text004"), "Step10", () => Advance(10));
        play = Button(Controls, DSNText.Russian("Viewer.ExecutionPanel.Text005"), "Play", () => { if (IsRunning) Pause(); else if (Session is not null) { UILocalization.Set(diagnostic, "Text", () => ""); timer.Start(); Refresh(); } });
        Button(Controls, DSNText.Russian("Viewer.ExecutionPanel.Text006"), "ReloadInc", () => { RefreshImplementations(); if (document is not null) { if (genericMode || preparedFromDSN) PrepareData(); else Bind(document, activeProfile); } });
        Button(Controls, DSNText.Russian("Viewer.ExecutionPanel.Text007"), "SaveStateHistory", SaveStateHistory);
        Button(Controls, DSNText.Russian("Report.Action"), "ExportReport", ExportReport);
        Button(Controls, DSNText.Russian("Report.Open"), "OpenReport", OpenReport).IsEnabled = false;
        Button(Controls, DSNText.Russian("Native.Action"), "ExportNative", () => ExportNative());
        foreach (var profileEntry in availableProfiles) profileChoice.Items.Add(UILocalization.Choice(DSNText.Russian("Viewer.ExecutionPanel.Text008", profileEntry.Profile.Id)));
        profileChoice.Items.Add(UILocalization.Choice(DSNText.Russian("Viewer.ExecutionPanel.Text010"))); profileChoice.SelectedIndex = 0;
        NameControl(profileChoice, "ExecutionProfile"); Controls.Children.Add(profileChoice);
        RefreshImplementations(); NameControl(implementationChoice, "ExecutionImplementation"); Controls.Children.Add(implementationChoice);
        implementationChoice.DropDownOpened += (_, _) => Pause();
        implementationChoice.SelectionChanged += (_, _) => { if (updatingImplementation || implementationChoice.SelectedItem is not string selected) return; activeImplementation = selected; if (document is not null) { if (genericMode || preparedFromDSN) PrepareData(); else Bind(document, activeProfile); } };
        profileChoice.DropDownOpened += (_, _) => Pause();
        profileChoice.SelectionChanged += (_, _) => { if (!updatingProfile && document is not null) { if (profileChoice.SelectedIndex >= 0 && profileChoice.SelectedIndex < availableProfiles.Length) Bind(document, availableProfiles[profileChoice.SelectedIndex].File); else BindUser(document); } };
        Controls.Children.Add(counter);
        Controls.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Text012"), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text012")))));
        timer.Tick += (_, _) => { try { AdvancePlayback(1); } catch (Exception error) { Fail(error); } };

        SetDock(diagnostic, Dock.Bottom); Children.Add(diagnostic); Children.Add(tabs);
        var inputPanel = new StackPanel { Margin = new Thickness(10) };
        inputPanel.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Text013"), FontSize = 16, FontWeight = FontWeights.SemiBold }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text013")))));
        inputPanel.Children.Add(inputNames);
        var entry = new StackPanel { Orientation = Orientation.Horizontal }; entry.Children.Add(inputValue);
        Button(entry, DSNText.Russian("Viewer.ExecutionPanel.Text014"), "ApplyInput", ApplyInput);
        Button(entry, DSNText.Russian("Viewer.ExecutionPanel.Text015"), "InputToScenario", InputToScenario); inputPanel.Children.Add(entry);
        var bitGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 0) };
        for (var i = 0; i < 16; i++)
        {
            var bit = i;
            bits[i] = UILocalization.With<global::System.Windows.Controls.CheckBox>(new CheckBox { Content = DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text016"), i), Margin = new Thickness(2, 4, 2, 4) }, ("Content", () => (object?)(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text016"), i))));
            NameControl(bits[i], DSNText.Concat("Bit", i));
            bits[i].Click += (_, _) =>
            {
                if (updatingBits) return;
                Pause();
                try
                {
                    var value = ExecutionSession.ParseWord(inputValue.Text);
                    value = bits[bit].IsChecked == true ? (ushort)(value | (1 << bit)) : (ushort)(value & ~(1 << bit));
                    UILocalization.Set(inputValue, "Text", () => DSNText.Concat("0x", value.ToString("X4"))); UpdateBits(value);
                }
                catch (Exception error) { Fail(error); }
            };
            bitGrid.Children.Add(bits[i]);
        }
        inputPanel.Children.Add(bitGrid); inputPanel.Children.Add(inputHint); inputPanel.Children.Add(scenario);
        inputPanel.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Text017"), TextWrapping = TextWrapping.Wrap }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text017")))));
        AddTab(DSNText.Russian("Viewer.ExecutionPanel.Text018"), inputPanel);
        var block = new DockPanel { Margin = new Thickness(10) }; SetDock(Parts, Dock.Top); block.Children.Add(Parts);
        block.Children.Add(new ScrollViewer { Content = BlockDetails, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); AddTab(DSNText.Russian("Viewer.ExecutionPanel.Text019"), block);
        initialGrid = Table(false); initialGrid.ItemsSource = initialRows; NameControl(initialGrid, "InitialGrid");
        initialGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridCheckBoxColumn>(new DataGridCheckBoxColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text020"), Binding = UILocalization.Binding(new Binding(nameof(InitialWordRow.Override))) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text020")))));
        initialGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text021"), Binding = UILocalization.Binding(new Binding(nameof(InitialWordRow.Name))), IsReadOnly = true }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text021")))));
        initialGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text022"), Binding = UILocalization.Binding(new Binding(nameof(InitialWordRow.Value))), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text022")))));
        var initialPanel = new DockPanel { Margin = new Thickness(8) };
        var initialHelp = UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Text023"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text023"))));
        SetDock(initialHelp, Dock.Top); initialPanel.Children.Add(initialHelp); initialPanel.Children.Add(initialGrid); AddTab(DSNText.Russian("Viewer.ExecutionPanel.Text024"), initialPanel);
        watchGrid = Table(false); watchGrid.ItemsSource = watchRows; NameControl(watchGrid, "WatchGrid");
        watchGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridCheckBoxColumn>(new DataGridCheckBoxColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text025"), Binding = UILocalization.Binding(new Binding(nameof(WatchRow.Watch))) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text025")))));
        watchGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text021"), Binding = UILocalization.Binding(new Binding(nameof(WatchRow.Name))), IsReadOnly = true }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text021")))));
        watchGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text026"), Binding = UILocalization.Binding(new Binding(nameof(WatchRow.Value))), IsReadOnly = true }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text026")))));
        watchGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = "Δ", Binding = UILocalization.Binding(new Binding(nameof(WatchRow.Changed))), IsReadOnly = true }, ("Header", () => (object?)("Δ"))));
        watchGrid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(() => { watchGrid.CommitEdit(); RebuildStateHistory(); });
        AddTab(DSNText.Russian("Viewer.ExecutionPanel.Text027"), watchGrid);
        readinessGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text028"), Binding = UILocalization.Binding(new Binding(nameof(FunctionReadiness.Function))) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text028")))));
        readinessGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text029"), Binding = UILocalization.Binding(new Binding(nameof(FunctionReadiness.Instances))) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text029")))));
        readinessGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text030"), Binding = UILocalization.Binding(new Binding(nameof(FunctionReadiness.Diagnostic))), Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = WrappedCell() }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text030")))));
        AddTab(DSNText.Russian("Viewer.ExecutionPanel.Text031"), readinessGrid);
        BuildScenarioTab();
        BuildSessionTab();
        BuildStopTab();
        BuildExpectationTab();
        BuildDataTab();
        inputNames.SelectionChanged += (_, _) => RefreshInput();
        tabs.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, tabs)) { if (tabs.SelectedIndex == 3) watchGrid.Items.Refresh(); UpdateSessionDetails(); } };
        inputNames.DropDownOpened += (_, _) => Pause();
        inputValue.GotKeyboardFocus += (_, _) => Pause();
        initialGrid.BeginningEdit += (_, _) => Pause();
        scenario.Click += (_, _) => Pause();
        StateHistory.ItemsSource = historyTable.DefaultView;
        BuildHistoryView();
        Refresh();
    }

    private void NameControl(FrameworkElement element, string name) { element.Name = name; RegisterName(name, element); }
    private Button Button(Panel panel, string text, string name, Action action)
    {
        var button = UILocalization.With<global::System.Windows.Controls.Button>(new Button { Content = text, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 3) }, ("Content", () => (object?)(text)));
        NameControl(button, name); executionButtons.Add(button);
        button.Click += (_, _) => { try { action(); } catch (Exception error) { Fail(error); } };
        panel.Children.Add(button); return button;
    }
    private void AddTab(string header, UIElement content) => tabs.Items.Add(UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = header, Content = content }, ("Header", () => (object?)(header))));
    private static Style WrappedCell()
    {
        var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap)); return style;
    }
    private static DataGrid Table(bool readOnly = true) => new()
    {
        AutoGenerateColumns = false, IsReadOnly = readOnly, CanUserAddRows = false, CanUserDeleteRows = false,
        EnableRowVirtualization = true, EnableColumnVirtualization = true, RowHeaderWidth = 0,
        HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        SelectionMode = DataGridSelectionMode.Single, FontSize = 12
    };
    public void Bind(LogicProgramDocument value, string? profileName = null)
    {
        if (profileName is null && !controlProfiles) { BindUser(value); return; }
        profileName ??= availableProfiles.FirstOrDefault().File ?? throw new InvalidDataException(DSNText.Russian("Profile.Required")); genericMode = false;
        BeginDataDocument(value);
        activeProfile = profileName; updatingProfile = true;
        var selectedProfile = Array.FindIndex(availableProfiles, p => p.File == profileName);
        profileChoice.SelectedIndex = selectedProfile >= 0 ? selectedProfile : availableProfiles.Length; updatingProfile = false;
        Pause(); document = value; preparedFromDSN = false; Session = null; Readiness = null; initialRows.Clear(); watchRows.Clear(); readinessGrid.ItemsSource = null;
        customScenario = false; eventRows.Clear();
        loadedSessionPath = null; selectedReference = null;
        stopRows.Clear(); stopWords.ItemsSource = null;
        expectationRows.Clear(); expectationWords.ItemsSource = null;
        inputNames.ItemsSource = null;
        AnalyzeData();
        try
        {
            var profile = DSNProgramBinder.ReadJson<ExecutionProfile>(ExecutionSession.ProfilePath(assetsRoot, profileName));
            var library = ExecutionImplementation.Load(assetsRoot, profile, activeImplementation);
            Readiness = ProgramReadiness.Analyze(value, library, BindingCatalog.FromLibrary(library));
            readinessGrid.ItemsSource = Readiness.Functions.OrderBy(f => f.CompilesInIsolation).ThenBy(f => f.Function, StringComparer.Ordinal).ToArray();
            ExecutionSchedule.Create(value, profile);
            Session = ExecutionSession.FromAssets(value, assetsRoot, profileName, activeImplementation);
            AttachSession();
        }
        catch (Exception error) { Session = null; UILocalization.Set(diagnostic, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text032"), error.Message)); tabs.SelectedIndex = 4; }
        RebuildStateHistory(); Refresh();
    }
    private void AttachSession(bool reset = true)
    {
        if (Session is null) return;
        initialRows.Clear(); watchRows.Clear();
        stopWords.ItemsSource = Session.Variables; expectationWords.ItemsSource = Session.Variables;
        var defaults = Session.InitialValues();
        foreach (var name in Session.Variables)
        {
            initialRows.Add(new() { Name = name, Value = DSNText.Concat("0x", defaults[name].ToString("X4")) });
            watchRows.Add(new() { Name = name, Watch = Session.Profile.Observe.Contains(name, StringComparer.OrdinalIgnoreCase) });
        }
        if (reset) Session.Reset(scenario.IsChecked == true); InitializeScenarioEditor();
        inputNames.ItemsSource = Session.Profile.InputWords; inputNames.SelectedIndex = 0; UILocalization.Set(diagnostic, "Text", () => "");
    }
    public void Pause() { timer.Stop(); UILocalization.Set(play, "Content", () => DSNText.Russian("Viewer.ExecutionPanel.Text005")); }
    private void Fail(Exception error) { Pause(); var draft = inputValue.Text; diagnostic.Foreground = Brushes.DarkRed; UILocalization.Set(diagnostic, "Text", () => error.Message); Refresh(); UILocalization.Set(inputValue, "Text", () => draft); }
    private void Reset()
    {
        Pause(); if (Session is null) return;
        if (customScenario && scenario.IsChecked == true) { ApplyEditedScenario(); return; }
        initialGrid.CommitEdit(DataGridEditingUnit.Cell, true); initialGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var values = initialRows.Where(r => r.Override).ToDictionary(r => r.Name, r => ExecutionSession.ParseWord(r.Value));
        Session.Reset(scenario.IsChecked == true, values); UILocalization.Set(diagnostic, "Text", () => ""); RebuildStateHistory(); Refresh();
    }
    private void Advance(int count) { Pause(); AdvancePlayback(count); }
    private void ApplyInput()
    {
        Pause();
        if (Session is null || inputNames.SelectedItem is not string name) return;
        var value = ExecutionSession.ParseWord(inputValue.Text); Session.QueueInput(name, value);
        UpdateBits(value); UILocalization.Set(diagnostic, "Text", () => ""); Refresh();
    }
    private void UpdateBits(ushort value)
    {
        updatingBits = true; for (var i = 0; i < bits.Length; i++) bits[i].IsChecked = (value & (1 << i)) != 0; updatingBits = false;
    }
    private void RefreshInput()
    {
        if (Session is null || inputNames.SelectedItem is not string name) return;
        var value = Session.PendingInputs.TryGetValue(name, out var pendingValue) ? pendingValue : Session.ReadWord(name);
        UILocalization.Set(inputValue, "Text", () => DSNText.Concat("0x", value.ToString("X4"))); UpdateBits(value);
    }
    private void Refresh()
    {
        foreach (var button in executionButtons)
        {
            if (nativeBusy) { button.IsEnabled = false; continue; }
            if (button.Name == "OpenReport") { button.IsEnabled = lastReport is not null && File.Exists(lastReport); continue; }
            button.IsEnabled = Session is not null && (!Session.Faulted || button.Name is "ExportReport" or "Reset" or "ReloadInc" or "SaveStateHistory" or "LoadScenario" or "ApplyScenario" or "SaveScenario" or "LoadSession" or "OpenControlScenario" or "ApplyExpectationScenario" or "ShowExpectationReport") || button.Name is "ReloadInc" or "LoadSession" or "LoadScenario" && document is not null;
            if (button.Name is "Step" or "Step10" or "Play" && Session is not null && Session.Tick >= Session.TickLimit) button.IsEnabled = false;
        }
        inputNames.IsEnabled = inputValue.IsEnabled = Session is not null; foreach (var bit in bits) bit.IsEnabled = Session is not null;
        scenarioTitle.IsEnabled = scenarioSteps.IsEnabled = eventGrid.IsEnabled = Session is not null;
        UILocalization.Set(play, "Content", () => IsRunning ? DSNText.Russian("Viewer.ExecutionPanel.Text033") : DSNText.Russian("Viewer.ExecutionPanel.Text005"));
        UILocalization.Set(counter, "Text", () => Session is null ? DSNText.Russian("Viewer.ExecutionPanel.Text034") : DSNText.Russian("Viewer.ExecutionPanel.Text035", Session.Tick, Session.TickLimit));
        UILocalization.Set(ProfileNotice, "Text", () => Session is null ? DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text036"), diagnostic.Text) :
            DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text038"), Session.Profile.Id), " · "), (Session.UseScenarioEvents ? DSNText.Russian("Viewer.ExecutionPanel.Text039") : DSNText.Russian("Viewer.ExecutionPanel.Text040"))), "\n"), Session.Architecture), DSNText.Russian("Viewer.ExecutionPanel.Text041")), Session.LibrarySha256[..12]));
        if (Session?.Faulted == true) UILocalization.Append(ProfileNotice, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Text042"));
        if (Session?.Profile.RandomTestData is { Length: > 0 }) UILocalization.Append(ProfileNotice, "Text", () => DSNText.Concat("\n", DSNText.Russian("Random.ActiveNotice")));
        if (Session?.ScenarioTitle is { } title) UILocalization.Append(ProfileNotice, "Text", () => DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text043"), title), (Session.Tick == Session.TickLimit ? DSNText.Russian("Viewer.ExecutionPanel.Text044") : "")));
        if (Session is not null) UILocalization.Append(ProfileNotice, "Text", () => DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text045"), (Session.Schedule.ReferenceNamesAffectOrder ? DSNText.Russian("Viewer.ExecutionPanel.Text046") : DSNText.Russian("Viewer.ExecutionPanel.Text047"))), DSNText.Russian("Viewer.ExecutionPanel.Text048")));
        if (Session?.VerifyExpectations() is { Total: > 0 } verification) UILocalization.Append(ProfileNotice, "Text", () => DSNText.Concat("\n", ExecutionSession.VerificationText(verification)));
        if (Session?.LastStop is { } hit && !IsRunning) UILocalization.Append(ProfileNotice, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Text049", hit.Tick, hit.Matches[0].Word));
        if (Session is null && Readiness is not null)
            UILocalization.Set(ProfileNotice, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text050", Readiness.Blocks, Readiness.Types, Readiness.SourceInstances, Readiness.Blocks), DSNText.Russian("Viewer.ExecutionPanel.Text051", Readiness.CompilableInstances, Readiness.Blocks)));
        UILocalization.Set(ProfileNotice, "ToolTip", () => Session is null ? diagnostic.Text : DSNText.Concat(DSNText.Join("\n", Session.Profile.Assumptions), DSNText.Russian("Viewer.ExecutionPanel.Text052")));
        UILocalization.Set(inputHint, "Text", () => Session is null ? "" : DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text053"), (Session.PendingInputs.Count == 0 ? DSNText.Russian("Viewer.ExecutionPanel.Text054") : DSNText.Join(", ", Session.PendingInputs.Select(p => DSNText.Concat(DSNText.Concat(p.Key, "="), ExecutionSession.FormatWord(p.Value)))))));
        if (Session is not null)
            foreach (var row in watchRows) { row.Value = ExecutionSession.FormatWord(Session.ReadWord(row.Name)); row.Changed = Session.Changed.Contains(row.Name) ? "●" : ""; }
        if (tabs.SelectedIndex == 3) watchGrid.Items.Refresh();
        RefreshInput(); UpdateStateHistory(); RefreshVariableTimelines(); RefreshStops(); RefreshExpectations(); UpdateSessionDetails(); StateChanged?.Invoke();
    }
    internal void RebuildStateHistory()
    {
        historyNames = watchRows.Where(r => r.Watch).Select(r => r.Name).ToArray();
        historyTable.Clear(); historyTable.Columns.Clear(); StateHistory.Columns.Clear();
        historyTable.Columns.Add(DSNText.Russian("Viewer.ExecutionPanel.Text055"), typeof(long)); StateHistory.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text055"), Binding = UILocalization.Binding(new Binding(DSNText.Russian("Viewer.ExecutionPanel.Text056"))) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text055")))));
        for (var i = 0; i < historyNames.Length; i++)
        {
            var column = DSNText.Concat("V", i); historyTable.Columns.Add(column, typeof(string));
            StateHistory.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = historyNames[i], Binding = UILocalization.Binding(new Binding(DSNText.Concat(DSNText.Concat("[", column), "]"))) }, ("Header", () => (object?)(historyNames[i]))));
        }
        UpdateStateHistory();
        RefreshVariableTimelines();
    }
    private void UpdateStateHistory()
    {
        if (Session is null) return;
        if (historyTable.Rows.Count < Session.Samples.Count)
        {
            historyTable.BeginLoadData();
            try
            {
                for (var i = historyTable.Rows.Count; i < Session.Samples.Count; i++)
                {
                    var sample = Session.Samples[i];
                    historyTable.Rows.Add(new object[] { sample.Tick }.Concat(historyNames.Select(n => (object)ExecutionSession.FormatWord(sample.Words[n]))).ToArray());
                }
            }
            finally { historyTable.EndLoadData(); }
        }
        if (StateHistory.Items.Count > 0) StateHistory.ScrollIntoView(StateHistory.Items[^1]);
    }
    private void SaveStateHistory()
    {
        Pause(); if (Session is null) return;
        var directory = Path.Combine(assetsRoot, "results"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DSNText.Concat(DSNText.Concat("interactive-state-history-", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")), ".json"));
        File.WriteAllText(path, JsonSerializer.Serialize(new { Profile = Session.Profile.Id, Session.Profile.CompatibilityConfirmed,
            Session.Profile.Assumptions, TimeUnit = DSNText.Russian("Execution.ScenarioRunner.Text001"), Session.UseScenarioEvents, Session.LibrarySha256,
            Session.Identity, Session.ScenarioTitle, Session.ScenarioSha256, Session.ManualInputs,
            StopConditions = Session.StopConditions, Session.LastStop,
            Verification = Session.VerifyExpectations(),
            Order = Session.Bound.Program.InstanceOrder, Observed = historyNames, Session.Samples,
            ProcessorHistory = Session.CaptureProcessorHistory() }, DSNProgramBinder.JsonOptions));
        UILocalization.Set(diagnostic, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Text057"), path));
        diagnostic.Foreground = Brushes.DarkSlateGray;
    }
    internal void SelectBlock(string? reference) { selectedReference = reference; tabs.SelectedIndex = 1; }
}
