// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DSN.Core;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed class RandomRangeRow(string code, int minimum, int maximum)
{
    public string Code { get; } = code;
    public string Label => DSNText.Russian("Random.Range." + Code);
    public string Minimum { get; set; } = minimum.ToString(CultureInfo.InvariantCulture);
    public string Maximum { get; set; } = maximum.ToString(CultureInfo.InvariantCulture);
}
internal sealed partial class ExecutionPanel
{
    private readonly TextBox randomSeed = new() { Text = "1", Width = 115, MaxLength = 128, Margin = new Thickness(5, 0, 0, 0) };
    private readonly DataGrid randomRanges = Table(false);
    private readonly ObservableCollection<RandomRangeRow> randomRangeRows = [];
    private readonly List<RandomTestBatch> pendingRandomBatches = [];
    private readonly TextBlock randomNotice = new() { TextWrapping = TextWrapping.Wrap };

    private void BuildRandomSettings(Panel header)
    {
        NameControl(randomSeed, "RandomSeed"); NameControl(randomRanges, "RandomRanges"); NameControl(randomNotice, "RandomNotice");
        var defaults = RandomTestSettings.Default;
        foreach (var (code, range) in new[] { ("Word", defaults.Word), ("Mask", defaults.Mask), ("Timer", defaults.Timer), ("Selection", defaults.Selection) })
            randomRangeRows.Add(new(code, range.Minimum, range.Maximum));
        var body = new StackPanel();
        var seed = new WrapPanel { Margin = new Thickness(0, 4, 0, 5) };
        seed.Children.Add(UILocalization.With(new TextBlock { VerticalAlignment = VerticalAlignment.Center }, ("Text", () => DSNText.Russian("Random.Seed"))));
        seed.Children.Add(randomSeed); body.Children.Add(seed);
        randomRanges.ItemsSource = randomRangeRows; randomRanges.CanUserResizeRows = false; randomRanges.MaxHeight = 135;
        randomRanges.Columns.Add(UILocalization.With(new DataGridTextColumn { Binding = UILocalization.Binding(new Binding(nameof(RandomRangeRow.Label))), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) }, ("Header", () => DSNText.Russian("Random.Category"))));
        randomRanges.Columns.Add(UILocalization.With(new DataGridTextColumn { Binding = new Binding(nameof(RandomRangeRow.Minimum)), Width = 80 }, ("Header", () => DSNText.Russian("Random.Minimum"))));
        randomRanges.Columns.Add(UILocalization.With(new DataGridTextColumn { Binding = new Binding(nameof(RandomRangeRow.Maximum)), Width = 80 }, ("Header", () => DSNText.Russian("Random.Maximum"))));
        body.Children.Add(randomRanges);
        body.Children.Add(UILocalization.With(new TextBlock { TextWrapping = TextWrapping.Wrap }, ("Text", () => DSNText.Russian("Random.RangesHint"))));
        var expander = UILocalization.With(new Expander { Content = body, Margin = new Thickness(0, 3, 0, 3) }, ("Header", () => DSNText.Russian("Random.Settings")));
        NameControl(expander, "RandomSettings"); header.Children.Add(expander); header.Children.Add(randomNotice);
    }
    private RandomTestDraft CaptureRandomSettings()
    {
        if (!randomRanges.CommitEdit(DataGridEditingUnit.Cell, true) || !randomRanges.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidDataException(DSNText.Russian("Random.FinishEdit"));
        return new(randomSeed.Text, randomRangeRows.ToDictionary(r => r.Code, r => new TestDataRangeDraft(r.Minimum, r.Maximum), StringComparer.Ordinal));
    }
    private bool RandomSettingsChanged()
    {
        var draft = CaptureRandomSettings(); var defaults = RandomTestSettings.Default;
        return draft.Seed != "1" || new[] { ("Word", defaults.Word), ("Mask", defaults.Mask), ("Timer", defaults.Timer), ("Selection", defaults.Selection) }
            .Any(p => draft.Ranges[p.Item1].Minimum != p.Item2.Minimum.ToString(CultureInfo.InvariantCulture) || draft.Ranges[p.Item1].Maximum != p.Item2.Maximum.ToString(CultureInfo.InvariantCulture));
    }
    private void RestoreRandomSettings(RandomTestDraft? draft)
    {
        if (draft is null) return;
        RandomTestData.Validate(draft); randomSeed.Text = draft.Seed;
        foreach (var row in randomRangeRows) { row.Minimum = draft.Ranges[row.Code].Minimum; row.Maximum = draft.Ranges[row.Code].Maximum; }
        randomRanges.Items.Refresh();
    }
    private RandomTestSettings ReadRandomSettings()
    {
        if (!randomRanges.CommitEdit(DataGridEditingUnit.Cell, true) || !randomRanges.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidDataException(DSNText.Russian("Random.FinishEdit"));
        if (!uint.TryParse(randomSeed.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
            throw new InvalidDataException(DSNText.Russian("Random.InvalidSeed"));
        var ranges = new Dictionary<string, TestDataRange>(StringComparer.Ordinal);
        foreach (var row in randomRangeRows)
        {
            if (!DSNProgramBinder.TryConstant(row.Minimum.Trim(), out var min) || !DSNProgramBinder.TryConstant(row.Maximum.Trim(), out var max))
                throw new InvalidDataException(DSNText.Russian("Random.InvalidRanges"));
            ranges.Add(row.Code, new(min, max));
        }
        var result = new RandomTestSettings(seed, ranges["Word"], ranges["Mask"], ranges["Timer"], ranges["Selection"]);
        RandomTestData.Validate(result); return result;
    }
    private void FillRandomData()
    {
        if (document is null) throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Data.Text016"));
        CommitData(); var settings = ReadRandomSettings(); var assets = DataAssets();
        var parameters = ProgramDataContext.Parameters(document, assets.Library, assets.Bindings, assets.Semantics, assets.Source);
        var analysis = VariableAnalyzer.Analyze(document, assets.Library, assets.Bindings, assets.Semantics, assets.Source);
        var batch = RandomTestData.Generate(document.SourceSha256, parameters, analysis, dataDrafts, settings);
        if (batch.GeneratedValues.Count == 0) { UILocalization.Set(dataNotice, "Text", () => DSNText.Russian("Random.NoMissing")); return; }
        if ((assets.Source?.RandomTestData?.Length ?? 0) + pendingRandomBatches.Count >= 128)
            throw new InvalidDataException(DSNText.Russian("Random.TooManyBatches"));
        // План полностью проверен до изменения первого поля; машина не подготавливается.
        foreach (var row in dataRows)
            if (batch.GeneratedValues.TryGetValue(row.Parameter.Key, out var value))
                row.Value = dataDrafts[row.Parameter.Key] = value.ToString(CultureInfo.InvariantCulture);
        pendingRandomBatches.Add(batch); parameterGrid.Items.Refresh(); DataPreparation = null;
        UILocalization.Set(dataNotice, "Text", () => DSNText.Russian("Random.Filled", batch.GeneratedValues.Count, settings.Seed));
        RefreshRandomNotice();
    }
    private ExecutionProfile WithRandomMetadata(ExecutionProfile context)
    {
        var batches = (context.RandomTestData ?? []).Concat(pendingRandomBatches).ToArray();
        context.RandomTestData = batches.Length == 0 ? null : batches;
        if (batches.Length > 0) context.Assumptions = context.Assumptions.Append(DSNText.Russian("Random.ProvenanceNote")).Distinct(StringComparer.Ordinal).ToArray();
        RandomTestData.Validate(context.RandomTestData); return context;
    }
    private void RefreshRandomNotice()
    {
        var count = (importedDataContext?.RandomTestData?.Length ?? 0) + pendingRandomBatches.Count;
        UILocalization.Set(randomNotice, "Text", () => count == 0 ? "" : DSNText.Russian("Random.HistoryNotice", count));
    }
}
