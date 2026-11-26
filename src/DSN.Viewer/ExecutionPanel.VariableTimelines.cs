// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Windows;
using System.Windows.Controls;

namespace DSN.Viewer;

internal sealed partial class ExecutionPanel
{
    public TabControl HistoryView { get; } = new();
    public VariableTimelineView VariableTimelines { get; } = new();

    private void BuildHistoryView()
    {
        NameControl(HistoryView, "HistoryView");
        HistoryView.Items.Add(UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Viewer.ExecutionPanel.VariableTimelines.Text001"), Content = StateHistory }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.VariableTimelines.Text001")))));
        HistoryView.Items.Add(UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Viewer.ExecutionPanel.VariableTimelines.Text002"), Content = VariableTimelines }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.VariableTimelines.Text002")))));
        HistoryView.SelectedIndex = 0;
    }

    private void RefreshVariableTimelines() => VariableTimelines.SetData(Session?.Samples, historyNames);
}
