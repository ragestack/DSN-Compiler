// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed class StopRow
{
    public bool Enabled { get; set; } = true;
    public string Word { get; set; } = "";
    public StopComparison Comparison { get; set; }
    public string Value { get; set; } = "0";
}
internal sealed class StopOperationChoice : System.ComponentModel.INotifyPropertyChanged
{
    public StopComparison Code { get; }
    private readonly string label;
    public string Label => DSNText.Translate(label);
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public StopOperationChoice(StopComparison code, string text)
    {
        Code = code; label = text;
        UILocalization.Observe(this, () => PropertyChanged?.Invoke(this, new(nameof(Label))));
    }
}

internal sealed partial class ExecutionPanel
{
    private readonly ObservableCollection<StopRow> stopRows = [];
    private readonly DataGrid stopGrid = Table(false);
    private readonly DataGridComboBoxColumn stopWords = UILocalization.With<global::System.Windows.Controls.DataGridComboBoxColumn>(new() { Header = DSNText.Russian("Viewer.ExecutionPanel.Text021"), SelectedItemBinding = UILocalization.Binding(new Binding(nameof(StopRow.Word))), Width = 130 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text021"))));
    private readonly TextBlock stopSummary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    private void BuildStopTab()
    {
        NameControl(stopGrid, "StopGrid"); NameControl(stopSummary, "StopSummary");
        var panel = new DockPanel { Margin = new Thickness(8) };
        var header = new StackPanel();
        var explanations = new StackPanel();
        header.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Stops.Text001"), FontSize = 16, FontWeight = FontWeights.SemiBold }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text001")))));
        explanations.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Stops.Text002"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text002")))));
        var buttons = new WrapPanel();
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Stops.Text003"), "AddStop", () =>
        {
            Pause();
            if (stopRows.Count >= ExecutionSession.MaxStopConditions) throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text004"));
            stopRows.Add(new() { Word = (watchGrid.SelectedItem as WatchRow)?.Name ?? inputNames.SelectedItem as string ?? Session?.Variables.FirstOrDefault() ?? "" });
            RefreshStops();
        });
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text004"), "DeleteStop", () => { Pause(); if (stopGrid.SelectedItem is StopRow row) stopRows.Remove(row); RefreshStops(); });
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Text014"), "ApplyStops", ApplyStops);
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Stops.Text005"), "ClearStops", () => { Pause(); Session?.ConfigureStops([]); stopRows.Clear(); UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Stops.Text006")); Refresh(); });
        header.Children.Add(buttons);
        explanations.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Stops.Text007"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text007")))));
        var help = new ScrollViewer { Content = explanations, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.SizeChanged += (_, _) => help.MaxHeight = Math.Max(45, panel.ActualHeight - 220);
        header.Children.Add(help); SetDock(header, Dock.Top); panel.Children.Add(header);
        var summary = new ScrollViewer { Content = stopSummary, MaxHeight = 105, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SetDock(summary, Dock.Bottom); panel.Children.Add(summary);
        stopGrid.ItemsSource = stopRows;
        stopGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridCheckBoxColumn>(new DataGridCheckBoxColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Stops.Text008"), Binding = UILocalization.Binding(new Binding(nameof(StopRow.Enabled))), Width = 40 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text008")))));
        stopGrid.Columns.Add(stopWords);
        stopGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridComboBoxColumn>(new DataGridComboBoxColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Stops.Text009"), SelectedValueBinding = UILocalization.Binding(new Binding(nameof(StopRow.Comparison))),
            DisplayMemberPath = nameof(StopOperationChoice.Label), SelectedValuePath = nameof(StopOperationChoice.Code), Width = 105,
            ItemsSource = Enum.GetValues<StopComparison>().Select(c => new StopOperationChoice(c, ExecutionSession.StopOperationText(c))).ToArray() }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text009")))));
        stopGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Stops.Text010"), Binding = UILocalization.Binding(new Binding(nameof(StopRow.Value))), Width = 106 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text010")))));
        stopGrid.BeginningEdit += (_, _) => Pause();
        panel.Children.Add(stopGrid); AddTab(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text011"), panel);
    }

    private void ApplyStops()
    {
        Pause(); if (Session is null) return;
        CommitStopGrid();
        var rules = new List<StopCondition>();
        for (var i = 0; i < stopRows.Count; i++)
        {
            var row = stopRows[i];
            try { rules.Add(new(row.Enabled, row.Word, row.Comparison, row.Comparison == StopComparison.Changed ? (ushort)0 : ExecutionSession.ParseWord(row.Value))); }
            catch (FormatException error) { throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text012", i + 1, row.Word, error.Message), error); }
        }
        Session.ConfigureStops(rules);
        diagnostic.Foreground = Brushes.DarkSlateGray; UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Stops.Text013")); Refresh();
    }

    private void CommitStopGrid()
    {
        if (!stopGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !stopGrid.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text014"));
    }

    private void RestoreStopEditor(StopConditionDraft[]? drafts)
    {
        stopRows.Clear();
        foreach (var row in drafts ?? Session?.StopConditions.Select(r => new StopConditionDraft(r.Enabled, r.Word, r.Comparison, DSNText.Concat("0x", r.Value.ToString("X4")))).ToArray() ?? [])
            stopRows.Add(new() { Enabled = row.Enabled, Word = row.Word, Comparison = row.Comparison, Value = row.Value });
    }

    private void RefreshStops()
    {
        stopGrid.IsEnabled = Session is not null;
        var rules = Session?.StopConditions;
        UILocalization.Set(stopSummary, "Text", () => rules is null ? DSNText.Russian("Viewer.ExecutionPanel.Stops.Text015") :
            DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text016", rules.Count(r => r.Enabled), rules.Count, stopRows.Count), (Session?.LastStop is { } hit ? DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text017", hit.Tick), DSNText.Join("\n", hit.Matches.Select(ExecutionSession.StopMatchText))) : DSNText.Russian("Viewer.ExecutionPanel.Stops.Text018"))));
    }

    private void AdvancePlayback(int count)
    {
        if (Session is null) return;
        var hit = Session.AdvanceUntilStop((int)Math.Min(count, Session.TickLimit - Session.Tick));
        if (hit is not null || Session.Tick >= Session.TickLimit) Pause();
        UILocalization.Set(diagnostic, "Text", () => hit is null ? "" : DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Stops.Text019", hit.Tick, ExecutionSession.StopMatchText(hit.Matches[0])), (hit.Matches.Count > 1 ? DSNText.Russian("Viewer.ExecutionPanel.Stops.Text020", hit.Matches.Count - 1) : "")));
        diagnostic.Foreground = Brushes.DarkSlateGray;
        Refresh();
        if (hit is not null) VariableTimelines.SelectTick((int)hit.Tick);
    }
}
