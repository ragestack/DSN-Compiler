// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed partial class ExecutionPanel
{
    private readonly TextBlock sessionDetails = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 21, Margin = new Thickness(0, 12, 0, 0) };
    private string? selectedReference;
    private string? loadedSessionPath;
    public event Action<string?>? RestoreSelection;

    private void BuildSessionTab()
    {
        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text001"), FontSize = 16, FontWeight = FontWeights.SemiBold }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text001")))));
        panel.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text002"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 10) }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text002")))));
        var buttons = new WrapPanel();
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text003"), "SaveSession", SaveSession);
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text004"), "LoadSession", OpenSession); panel.Children.Add(buttons);
        panel.Children.Add(sessionDetails);
        AddTab(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text005"), new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
    }

    private SessionViewState CaptureView()
    {
        CommitStopGrid();
        CommitExpectations();
        foreach (var grid in new[] { initialGrid, eventGrid, watchGrid })
            if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text006"));
        return new() { DataDraft = CaptureDataDraft(), ExpectationDrafts = expectationRows.Count == 0 ? null : expectationRows.Select(r => new ExpectationDraft(r.Tick, r.Word, r.Value, r.Mask)).ToArray(),
            StopDrafts = stopRows.Count == 0 ? null : stopRows.Select(r => new StopConditionDraft(r.Enabled, r.Word, r.Comparison, r.Value)).ToArray(),
            Timeline = VariableTimelines.Capture(HistoryView.SelectedIndex), Observed = watchRows.Where(r => r.Watch).Select(r => r.Name).ToArray(),
            Initial = initialRows.Select(r => new InitialWordDraft(r.Name, r.Override, r.Value)).ToArray(),
            ScenarioTitle = scenarioTitle.Text, ScenarioSteps = scenarioSteps.Text,
            Events = eventRows.Select(r => new InputEventDraft(r.Tick, r.Word, r.Value)).ToArray(),
            CustomScenario = customScenario, EventsOnReset = scenario.IsChecked == true,
            SelectedInput = inputNames.SelectedItem as string ?? "", InputText = inputValue.Text,
            SelectedBlock = selectedReference, SelectedTab = tabs.SelectedIndex };
    }

    private void SaveSession()
    {
        Pause(); if (Session is null) return;
        var view = CaptureView();
        var directory = Path.Combine(assetsRoot, "sessions"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat("session-", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)), "-"), Guid.NewGuid().ToString("N")[..8]), ".dsnsession"));
        SessionFile.Save(path, Session, view);
        loadedSessionPath = path; UpdateSessionDetails();
        diagnostic.Foreground = Brushes.DarkSlateGray; UILocalization.Set(diagnostic, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text007"), path));
    }

    private void OpenSession()
    {
        Pause();
        var directory = Path.Combine(assetsRoot, "sessions");
        var dialog = UILocalization.With<global::Microsoft.Win32.OpenFileDialog>(new OpenFileDialog { Filter = DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text008"), CheckFileExists = true,
            AddToRecent = false, InitialDirectory = Directory.Exists(directory) ? directory : assetsRoot }, ("Filter", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text008"))));
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) LoadSessionFrom(dialog.FileName);
    }

    internal void LoadSessionFrom(string path)
    {
        Pause(); if (document is null) throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Data.Text016"));
        var saved = SessionFile.Read(path);
        var replacement = saved.PreparedProfile is { } profile && (Session is null || saved.Identity != Session.Identity)
            ? PreparedProgramContext.Create(document, assetsRoot, profile, saved.Identity)
            : Session ?? throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text009"));
        var view = replacement.RestoreSession(saved);
        RefreshImplementations(replacement.Architecture);
        if (!ReferenceEquals(Session, replacement)) { AdoptPreparedSession(replacement); AttachSession(false); }
        Session = replacement;
        RestoreDataDraft(view.DataDraft);
        RestoreStopEditor(view.StopDrafts);
        RestoreExpectationDrafts(view.ExpectationDrafts);
        var rows = view.Initial.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var row in initialRows) { var restored = rows[row.Name]; row.Override = restored.Override; row.Value = restored.Value; }
        foreach (var row in watchRows) row.Watch = view.Observed.Contains(row.Name, StringComparer.OrdinalIgnoreCase);
        UILocalization.Set(scenarioTitle, "Text", () => view.ScenarioTitle); UILocalization.Set(scenarioSteps, "Text", () => view.ScenarioSteps);
        eventRows.Clear(); foreach (var row in view.Events) eventRows.Add(new() { Tick = row.Tick, Word = row.Word, Value = row.Value });
        customScenario = view.CustomScenario; scenario.IsChecked = view.EventsOnReset;
        inputNames.SelectedItem = view.SelectedInput;
        initialGrid.Items.Refresh(); watchGrid.Items.Refresh();
        selectedReference = view.SelectedBlock; RestoreSelection?.Invoke(view.SelectedBlock);
        tabs.SelectedIndex = view.SelectedTab;
        loadedSessionPath = path;
        RebuildStateHistory(); Refresh();
        VariableTimelines.Restore(view.Timeline); HistoryView.SelectedIndex = view.Timeline?.HistoryTab ?? 0;
        // Текст ещё не применённого ввода сохраняется отдельно от действующей очереди.
        UILocalization.Set(inputValue, "Text", () => view.InputText);
        try { UpdateBits(ExecutionSession.ParseWord(view.InputText)); } catch (FormatException) { }
        diagnostic.Foreground = Brushes.DarkSlateGray;
        UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text010", Session.Tick));
    }

    private void AdoptPreparedSession(ExecutionSession replacement)
    {
        Session = replacement; preparedFromDSN = true; genericMode = true; activeProfile = null;
        importedDataContext = ProgramDataContext.Clone(replacement.Profile); dataRows.Clear(); dataDrafts.Clear(); dataContext.IsChecked = false;
        updatingProfile = true; profileChoice.SelectedIndex = (controlProfiles ? 2 : 0); updatingProfile = false;
    }

    private void UpdateSessionDetails()
    {
        if (Session is null) { UILocalization.Set(sessionDetails, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text011")); return; }
        if (tabs.SelectedIndex != 6) return;
        var flags = Session.Flags;
        UILocalization.Set(sessionDetails, "Text", () => DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text012", Session.Tick, Session.TickLimit, Session.Samples.Count), (Session.Architecture == "RV32I" ? DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text013") :
                DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text014", Session.Architecture, flags.Zero, flags.Negative, flags.Overflow, flags.Carry))), DSNText.Join("\n", Session.Registers.Select(p => DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(p.Key, " = "), p.Value), " (0x"), p.Value.ToString("X8")), ")")))), DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text015")), (Session.PendingInputs.Count == 0 ? DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text016") : DSNText.Join("\n", Session.PendingInputs.Select(p => DSNText.Concat(DSNText.Concat(p.Key, " = "), ExecutionSession.FormatWord(p.Value)))))), (loadedSessionPath is null ? "" : DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text017"), loadedSessionPath))), (Session.Faulted ? DSNText.Russian("Viewer.ExecutionPanel.Sessions.Text018") : "")));
    }
}
