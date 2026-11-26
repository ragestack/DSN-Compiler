// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed class ScenarioEventRow
{
    public string Tick { get; set; } = "1";
    public string Word { get; set; } = "";
    public string Value { get; set; } = "0";
}

internal sealed partial class ExecutionPanel
{
    private readonly ObservableCollection<ScenarioEventRow> eventRows = [];
    private readonly TextBox scenarioTitle = UILocalization.With<global::System.Windows.Controls.TextBox>(new() { Text = DSNText.Russian("Execution.ExecutionScenario.Text001"), Padding = new Thickness(4), Margin = new Thickness(0, 4, 0, 6) }, ("Text", () => (object?)(DSNText.Russian("Execution.ExecutionScenario.Text001"))));
    private readonly TextBox scenarioSteps = UILocalization.With<global::System.Windows.Controls.TextBox>(new() { Text = "180", Width = 80, Padding = new Thickness(4) }, ("Text", () => (object?)("180")));
    private readonly DataGrid eventGrid = Table(false);
    private readonly DataGridComboBoxColumn eventWords = UILocalization.With<global::System.Windows.Controls.DataGridComboBoxColumn>(new() { Header = DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text001"), SelectedItemBinding = UILocalization.Binding(new Binding(nameof(ScenarioEventRow.Word))), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text001"))));
    private bool customScenario;

    private void BuildScenarioTab()
    {
        NameControl(scenarioTitle, "ScenarioTitle"); NameControl(scenarioSteps, "ScenarioSteps"); NameControl(eventGrid, "ScenarioEvents");
        var panel = new DockPanel { Margin = new Thickness(8) };
        var header = new StackPanel();
        header.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text002"), FontSize = 16, FontWeight = FontWeights.SemiBold }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text002")))));
        header.Children.Add(scenarioTitle);
        var duration = new StackPanel { Orientation = Orientation.Horizontal };
        duration.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text003"), VerticalAlignment = VerticalAlignment.Center }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text003"))))); duration.Children.Add(scenarioSteps); header.Children.Add(duration);
        header.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text004"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text004")))));
        var buttons = new WrapPanel();
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Data.Text005"), "LoadScenario", OpenScenario);
        UILocalization.Set(Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text005"), "SaveScenario", SaveScenario), "ToolTip", () => DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text006"));
        UILocalization.Set(Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text007"), "ApplyScenario", ApplyEditedScenario), "ToolTip", () => DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text008"));
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text009"), "ProfileScenario", InitializeScenarioEditor);
        header.Children.Add(buttons);
        var events = new WrapPanel();
        Button(events, DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text010"), "AddScenarioEvent", () => eventRows.Add(new() { Word = Session?.Profile.InputWords.FirstOrDefault() ?? "" }));
        Button(events, DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text004"), "DeleteScenarioEvent", () => { if (eventGrid.SelectedItem is ScenarioEventRow row) eventRows.Remove(row); });
        header.Children.Add(events);
        var scroll = new ScrollViewer { Content = header, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        // В небольшом окне таблица событий сохраняет место; пояснения можно прокрутить.
        panel.SizeChanged += (_, _) => scroll.MaxHeight = Math.Max(100, panel.ActualHeight - 120);
        SetDock(scroll, Dock.Top); panel.Children.Add(scroll);
        eventGrid.ItemsSource = eventRows;
        eventGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text055"), Binding = UILocalization.Binding(new Binding(nameof(ScenarioEventRow.Tick))), Width = 58 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text055")))));
        eventGrid.Columns.Add(eventWords);
        eventGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text026"), Binding = UILocalization.Binding(new Binding(nameof(ScenarioEventRow.Value))), Width = 100 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text026")))));
        panel.Children.Add(eventGrid); AddTab(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text011"), panel);
        scenarioTitle.GotKeyboardFocus += (_, _) => Pause(); scenarioSteps.GotKeyboardFocus += (_, _) => Pause(); eventGrid.BeginningEdit += (_, _) => Pause();
    }

    private void InitializeScenarioEditor()
    {
        Pause(); if (Session is null) return;
        UILocalization.Set(scenarioTitle, "Text", () => DSNText.Concat(DSNText.Russian("Execution.ExecutionSession.Persistence.Text020"), Session.Profile.Id)); UILocalization.Set(scenarioSteps, "Text", () => Math.Clamp(Session.Profile.Steps, 1, ExecutionSession.MaxTicks).ToString(CultureInfo.InvariantCulture));
        eventWords.ItemsSource = Session.Profile.InputWords; SetEventRows(Session.Profile.Events);
        SetExpectationRows(null);
    }

    private void SetEventRows(IEnumerable<InputEvent> events)
    {
        eventRows.Clear();
        foreach (var entry in events.OrderBy(e => e.Tick))
            foreach (var (name, value) in entry.Values)
                eventRows.Add(new() { Tick = entry.Tick.ToString(CultureInfo.InvariantCulture), Word = name, Value = DSNText.Concat("0x", value.ToString("X4")) });
    }

    private ExecutionScenario EditedScenario()
    {
        if (Session is null) throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text012"));
        foreach (var grid in new[] { initialGrid, eventGrid, watchGrid })
            if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text013"));
        if (!int.TryParse(scenarioSteps.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var steps))
            throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text014"));
        if (eventRows.Count > ScenarioFile.MaxAssignments) throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text015"));
        var events = new Dictionary<int, InputEvent>();
        foreach (var row in eventRows)
        {
            if (!int.TryParse(row.Tick.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text016"), row.Tick));
            if (!events.TryGetValue(tick, out var entry)) events[tick] = entry = new() { Tick = tick, Values = new(StringComparer.OrdinalIgnoreCase) };
            if (!entry.Values.TryAdd(row.Word, ExecutionSession.ParseWord(row.Value))) throw new InvalidDataException(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text017"), tick), ": "), row.Word));
        }
        return ScenarioFile.Validate(UILocalization.With<global::DSN.Execution.ExecutionScenario>(new() { Title = scenarioTitle.Text, Identity = Session.Identity, Steps = steps,
            InitialOverrides = initialRows.Where(r => r.Override).ToDictionary(r => r.Name, r => ExecutionSession.ParseWord(r.Value)),
            Events = events.Values.ToArray(), Observe = watchRows.Where(r => r.Watch).Select(r => r.Name).ToArray(), Expectations = EditedExpectations() }, ("Title", () => (object?)(scenarioTitle.Text))), Session);
    }

    private void ApplyEditedScenario()
    {
        Pause(); if (Session is null) return;
        var value = EditedScenario(); Session.ApplyScenario(value); customScenario = true; scenario.IsChecked = true;
        UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text018"));
        diagnostic.Foreground = Brushes.DarkSlateGray; RebuildStateHistory(); Refresh();
    }

    private void SaveScenario()
    {
        Pause(); if (Session is null) return;
        var value = EditedScenario();
        var directory = Path.Combine(assetsRoot, "scenarios"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DSNText.Concat(DSNText.Concat("scenario-", Guid.NewGuid().ToString("N")), ".json"));
        ScenarioFile.Save(path, value, Session);
        diagnostic.Foreground = Brushes.DarkSlateGray; UILocalization.Set(diagnostic, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text019"), path));
    }

    private void OpenScenario()
    {
        Pause();
        var directory = Path.Combine(assetsRoot, "scenarios");
        var dialog = UILocalization.With<global::Microsoft.Win32.OpenFileDialog>(new OpenFileDialog { Filter = DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text020"), CheckFileExists = true,
            AddToRecent = false, InitialDirectory = Directory.Exists(directory) ? directory : assetsRoot }, ("Filter", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text020"))));
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) LoadScenarioFrom(dialog.FileName);
    }

    internal void LoadScenarioFrom(string path)
    {
        Pause(); if (document is null) throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Data.Text016"));
        var source = ScenarioFile.Read(path);
        var replacement = source.PreparedProfile is { } profile && (Session is null || source.Identity != Session.Identity)
            ? PreparedProgramContext.Create(document, assetsRoot, profile, source.Identity, activeImplementation)
            : Session ?? throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text021"));
        var value = ScenarioFile.Validate(source, replacement);
        // Таблицы и текущая машина меняются только после полной проверки файла.
        replacement.ApplyScenario(value);
        if (!ReferenceEquals(Session, replacement)) { AdoptPreparedSession(replacement); AttachSession(false); }
        Session = replacement;
        var defaults = Session.InitialValues();
        foreach (var row in initialRows)
        {
            row.Override = value.InitialOverrides.TryGetValue(row.Name, out var initial);
            row.Value = DSNText.Concat("0x", (row.Override ? initial : defaults[row.Name]).ToString("X4"));
        }
        foreach (var row in watchRows) row.Watch = value.Observe.Contains(row.Name, StringComparer.OrdinalIgnoreCase);
        UILocalization.Set(scenarioTitle, "Text", () => value.Title); UILocalization.Set(scenarioSteps, "Text", () => value.Steps.ToString(CultureInfo.InvariantCulture));
        SetEventRows(value.Events); customScenario = true; scenario.IsChecked = true;
        SetExpectationRows(value.Expectations);
        initialGrid.Items.Refresh(); watchGrid.Items.Refresh(); tabs.SelectedIndex = 5;
        diagnostic.Foreground = Brushes.DarkSlateGray; UILocalization.Set(diagnostic, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Scenarios.Text022"), Path.GetFileName(path)));
        RebuildStateHistory(); Refresh();
    }

    private void InputToScenario()
    {
        Pause(); if (Session is null || inputNames.SelectedItem is not string name) return;
        var value = ExecutionSession.ParseWord(inputValue.Text);
        eventRows.Add(new() { Tick = (Session.Tick + 1).ToString(CultureInfo.InvariantCulture), Word = name, Value = DSNText.Concat("0x", value.ToString("X4")) });
        tabs.SelectedIndex = 5;
    }
}
