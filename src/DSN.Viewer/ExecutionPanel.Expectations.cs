// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DSN.Execution;
using Microsoft.Win32;

namespace DSN.Viewer;

internal sealed class ExpectationRow
{
    public string Tick { get; set; } = "0";
    public string Word { get; set; } = "";
    public string Value { get; set; } = "0";
    public string Mask { get; set; } = "";
}
internal sealed record ExpectationResultRow(int Tick, string Word, string Expected, string Actual, string Mask, string Result);

internal sealed partial class ExecutionPanel
{
    private readonly ObservableCollection<ExpectationRow> expectationRows = [];
    private readonly DataGrid expectationGrid = Table(false);
    private readonly DataGrid expectationReport = Table();
    private readonly DataGridComboBoxColumn expectationWords = UILocalization.With<global::System.Windows.Controls.DataGridComboBoxColumn>(new() { Header = DSNText.Russian("Viewer.ExecutionPanel.Text021"), SelectedItemBinding = UILocalization.Binding(new Binding(nameof(ExpectationRow.Word))), Width = 130 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text021"))));
    private readonly TextBlock expectationSummary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private readonly TabControl expectationTabs = new();

    private void BuildExpectationTab()
    {
        NameControl(expectationGrid, "ExpectationGrid"); NameControl(expectationReport, "ExpectationReport");
        NameControl(expectationSummary, "ExpectationSummary"); NameControl(expectationTabs, "ExpectationTabs");
        var panel = new DockPanel { Margin = new Thickness(8) }; var header = new StackPanel();
        header.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text001"), FontSize = 16, FontWeight = FontWeights.SemiBold }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text001")))));
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 3) };
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text002"), "AddExpectation", () =>
        {
            Pause();
            if (expectationRows.Count >= ScenarioFile.MaxExpectations) throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text003"));
            expectationRows.Add(new() { Tick = (Session?.Tick ?? 0).ToString(CultureInfo.InvariantCulture), Word = Session?.Variables.FirstOrDefault() ?? "" });
            expectationTabs.SelectedIndex = 0;
        });
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text004"), "DeleteExpectation", () => { Pause(); if (expectationGrid.SelectedItem is ExpectationRow row) expectationRows.Remove(row); });
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text005"), "ApplyExpectationScenario", ApplyEditedScenario);
        Button(buttons, DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text006"), "ShowExpectationReport", () => { expectationTabs.SelectedIndex = 1; RefreshExpectations(); });
        Button(buttons, controlProfiles ? DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text007") : DSNText.Russian("Viewer.ExecutionPanel.Data.Text005"), "OpenControlScenario", () =>
        {
            Pause();
            var dialog = UILocalization.With<global::Microsoft.Win32.OpenFileDialog>(new OpenFileDialog { Filter = DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text008"), CheckFileExists = true, AddToRecent = false,
                InitialDirectory = controlProfiles ? Path.Combine(assetsRoot, "samples", "scenarios") : assetsRoot }, ("Filter", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text008"))));
            if (dialog.ShowDialog(Window.GetWindow(this)) == true) { LoadScenarioFrom(dialog.FileName); tabs.SelectedIndex = 8; }
        });
        header.Children.Add(buttons);
        var help = new ScrollViewer { Content = UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text009"), TextWrapping = TextWrapping.Wrap }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text009")))),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        // В малом окне пояснение прокручивается, кнопки и результаты остаются видимыми.
        panel.SizeChanged += (_, _) => help.MaxHeight = Math.Max(34, panel.ActualHeight - 340);
        header.Children.Add(help);
        header.Children.Add(expectationSummary); SetDock(header, Dock.Top); panel.Children.Add(header);
        expectationGrid.ItemsSource = expectationRows;
        expectationGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text055"), Binding = UILocalization.Binding(new Binding(nameof(ExpectationRow.Tick))), Width = 48 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text055")))));
        expectationGrid.Columns.Add(expectationWords);
        expectationGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text010"), Binding = UILocalization.Binding(new Binding(nameof(ExpectationRow.Value))), Width = 90 }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text010")))));
        expectationGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Execution.VariableAnalysis.Text005"), Binding = UILocalization.Binding(new Binding(nameof(ExpectationRow.Mask))), Width = 88 }, ("Header", () => (object?)(DSNText.Russian("Execution.VariableAnalysis.Text005")))));
        expectationGrid.BeginningEdit += (_, _) => Pause();
        foreach (var (name, title, width) in new[] { ("Tick", DSNText.Russian("Viewer.ExecutionPanel.Text055"), 40), ("Word", DSNText.Russian("Viewer.ExecutionPanel.Text021"), 100), ("Expected", DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text011"), 58),
            ("Actual", DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text012"), 58), ("Mask", DSNText.Russian("Execution.VariableAnalysis.Text005"), 58), ("Result", DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text013"), 76) })
            expectationReport.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = title, Binding = UILocalization.Binding(new Binding(name)), Width = width }, ("Header", () => (object?)(title))));
        expectationReport.SelectionChanged += (_, _) =>
        {
            if (expectationReport.SelectedItem is ExpectationResultRow row && Session is not null && row.Tick < Session.Samples.Count) VariableTimelines.SelectTick(row.Tick);
        };
        expectationTabs.Items.Add(UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text014"), Content = expectationGrid }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text014")))));
        expectationTabs.Items.Add(UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text015"), Content = expectationReport }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text015")))));
        expectationTabs.SelectedIndex = 0; panel.Children.Add(expectationTabs); AddTab(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text016"), panel);
    }

    private void CommitExpectations()
    {
        if (!expectationGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !expectationGrid.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text017"));
    }
    private ScenarioExpectation[]? EditedExpectations()
    {
        CommitExpectations();
        if (expectationRows.Count == 0) return null;
        return expectationRows.Select((r, i) =>
        {
            if (!int.TryParse(r.Tick.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var tick))
                throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text018", i + 1));
            try { return new ScenarioExpectation(tick, r.Word, ExecutionSession.ParseWord(r.Value), string.IsNullOrWhiteSpace(r.Mask) ? null : ExecutionSession.ParseWord(r.Mask)); }
            catch (FormatException error) { throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text019", i + 1, error.Message), error); }
        }).ToArray();
    }
    private void SetExpectationRows(IEnumerable<ScenarioExpectation>? entries) => RestoreExpectationDrafts(entries?.Select(e =>
        new ExpectationDraft(e.Tick.ToString(CultureInfo.InvariantCulture), e.Word, DSNText.Concat("0x", e.Value.ToString("X4")), e.Mask is { } mask ? DSNText.Concat("0x", mask.ToString("X4")) : "")).ToArray());
    private void RestoreExpectationDrafts(IEnumerable<ExpectationDraft>? drafts)
    {
        expectationRows.Clear();
        foreach (var r in drafts ?? []) expectationRows.Add(new() { Tick = r.Tick, Word = r.Word, Value = r.Value, Mask = r.Mask });
    }
    private void RefreshExpectations()
    {
        expectationGrid.IsEnabled = Session is not null;
        var report = Session?.VerifyExpectations();
        UILocalization.Set(expectationSummary, "Text", () => report is null ? DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text020") : ExecutionSession.VerificationText(report));
        expectationReport.ItemsSource = report?.Checks.Select(c => new ExpectationResultRow(c.Tick, c.Word, c.Expected.ToString(CultureInfo.InvariantCulture),
            c.Actual?.ToString(CultureInfo.InvariantCulture) ?? "–", c.Mask is { } mask ? DSNText.Concat("0x", mask.ToString("X4")) : "–",
            c.Result == "PASS" ? DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text021") : c.Result == "FAIL" ? DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text022") : DSNText.Russian("Viewer.ExecutionPanel.Expectations.Text023"))).ToArray();
    }
}
