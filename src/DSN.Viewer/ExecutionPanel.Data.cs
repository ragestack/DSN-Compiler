// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DSN.Core;
using DSN.Execution;
using Microsoft.Win32;

namespace DSN.Viewer;

internal sealed class DataParameterRow
{
    public required DataParameter Parameter { get; init; }
    public string Name => Parameter.Name;
    public string Existing => Parameter.DisplayValue;
    public string Value { get; set; } = "";
}

internal sealed partial class ExecutionPanel
{
    private bool preparedFromDSN;
    private ExecutionProfile? importedDataContext;
    private readonly Dictionary<string, string> dataDrafts = new(StringComparer.Ordinal);
    private readonly ObservableCollection<DataParameterRow> dataRows = [];
    private readonly DataGrid dataGrid = Table();
    private readonly DataGrid parameterGrid = Table(false);
    private readonly TextBlock dataNotice = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly CheckBox dataContext = UILocalization.With<global::System.Windows.Controls.CheckBox>(new() { Content = DSNText.Russian("Viewer.ExecutionPanel.Data.Text001"), Margin = new Thickness(0, 4, 0, 4) }, ("Content", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text001"))));
    public ContextPreparation? DataPreparation { get; private set; }

    private void BuildDataTab()
    {
        var panel = new DockPanel { Margin = new Thickness(8) };
        var header = new StackPanel();
        var headerScroll = new ScrollViewer { Content = header, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.SizeChanged += (_, _) => headerScroll.MaxHeight = Math.Max(110, panel.ActualHeight - 140);
        SetDock(headerScroll, Dock.Top); panel.Children.Add(headerScroll);
        header.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.ExecutionPanel.Data.Text002"), TextWrapping = TextWrapping.Wrap }, ("Text", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text002")))));
        NameControl(dataContext, "DataUseContext"); dataContext.Visibility = controlProfiles ? Visibility.Visible : Visibility.Collapsed; header.Children.Add(dataContext);
        dataContext.Click += (_, _) => { Pause(); importedDataContext = null; AnalyzeData(); };
        var buttons = new WrapPanel(); header.Children.Add(buttons);
        foreach (var (title, name, action) in new (string, string, Action)[] {
            (DSNText.Russian("Viewer.ExecutionPanel.Data.Text003"), "CheckData", () => { CommitData(); AnalyzeData(); }), (DSNText.Russian("Viewer.ExecutionPanel.Data.Text004"), "PrepareData", PrepareData),
            (DSNText.Russian("Viewer.ExecutionPanel.Data.Text005"), "OpenDataContext", OpenDataContext), (DSNText.Russian("Viewer.ExecutionPanel.Data.Text006"), "SaveDataContext", SaveDataContext),
            (DSNText.Russian("Random.Button"), "FillRandomData", FillRandomData) })
        {
            var button = UILocalization.With<global::System.Windows.Controls.Button>(new Button { Content = title, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 4, 2) }, ("Content", () => (object?)(title)));
            NameControl(button, name); button.Click += (_, _) => { Pause(); try { action(); } catch (Exception error) { Fail(error); } }; buttons.Children.Add(button);
        }
        BuildRandomSettings(header);
        header.Children.Add(new ScrollViewer { Content = dataNotice, MaxHeight = 48, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var detail = new TabControl(); panel.Children.Add(detail);
        parameterGrid.ItemsSource = dataRows; NameControl(parameterGrid, "DataParameterGrid");
        parameterGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Data.Text007"), Binding = UILocalization.Binding(new Binding(nameof(DataParameterRow.Name))), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = WrappedCell() }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text007")))));
        parameterGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Data.Text008"), Binding = UILocalization.Binding(new Binding(nameof(DataParameterRow.Existing))), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = WrappedCell() }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text008")))));
        parameterGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = DSNText.Russian("Viewer.ExecutionPanel.Text020"), Binding = UILocalization.Binding(new Binding(nameof(DataParameterRow.Value))), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Text020")))));
        var parameterStyle = new Style(typeof(DataGridRow)); parameterStyle.Setters.Add(new Setter(ToolTipProperty, UILocalization.Binding(new Binding("Parameter.Purpose")))); parameterGrid.RowStyle = parameterStyle;
        parameterGrid.BeginningEdit += (_, _) => Pause();
        var parameters = UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Viewer.ExecutionPanel.Data.Text009"), Content = parameterGrid }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text009")))); NameControl(parameters, "DataParametersTab"); detail.Items.Add(parameters);
        NameControl(dataGrid, "VariableDataGrid");
        var rowStyle = new Style(typeof(DataGridRow)); rowStyle.Setters.Add(new Setter(ToolTipProperty, UILocalization.Binding(new Binding(nameof(VariableData.DisplayEvidence))))); dataGrid.RowStyle = rowStyle;
        foreach (var (title, property) in new[] { (DSNText.Russian("Viewer.ExecutionPanel.Data.Text010"), "DisplayName"), (DSNText.Russian("Viewer.ExecutionPanel.Data.Text011"), "DisplayType"), (DSNText.Russian("Viewer.ExecutionPanel.Data.Text012"), "DisplayRoles"), (DSNText.Russian("Viewer.ExecutionPanel.Data.Text013"), "DisplayBoolean") })
            dataGrid.Columns.Add(UILocalization.With<global::System.Windows.Controls.DataGridTextColumn>(new DataGridTextColumn { Header = title, Binding = UILocalization.Binding(new Binding(property)), ElementStyle = WrappedCell(), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }, ("Header", () => (object?)(title))));
        var types = UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Viewer.ExecutionPanel.Data.Text014"), Content = dataGrid }, ("Header", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text014")))); NameControl(types, "DataTypesTab"); detail.Items.Add(types);
        var tab = UILocalization.With<global::System.Windows.Controls.TabItem>(new TabItem { Header = DSNText.Russian("Execution.VariableAnalysis.Text003"), Content = panel }, ("Header", () => (object?)(DSNText.Russian("Execution.VariableAnalysis.Text003")))); NameControl(tab, "VariableDataTab"); tabs.Items.Add(tab);
    }

    private void BeginDataDocument(LogicProgramDocument value)
    {
        if (document?.SourceSha256 != value.SourceSha256)
        { dataDrafts.Clear(); dataRows.Clear(); pendingRandomBatches.Clear(); importedDataContext = null; dataContext.IsChecked = false; RefreshRandomNotice(); }
    }
    private void CommitData()
    {
        if (!parameterGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !parameterGrid.CommitEdit(DataGridEditingUnit.Row, true))
            throw new InvalidDataException(DSNText.Russian("Viewer.ExecutionPanel.Data.Text015"));
        foreach (var row in dataRows) dataDrafts[row.Parameter.Key] = row.Value;
    }
    private (ExecutionProfile Scope, IFunctionLibrary Library, BindingCatalog Bindings, FunctionSemantics Semantics, ExecutionProfile? Source) DataAssets()
    {
        var scope = UserProgramPreparation.StandardScope();
        var source = importedDataContext ?? (dataContext.IsChecked == true && activeProfile is not null ? DSNProgramBinder.ReadJson<ExecutionProfile>(ExecutionSession.ProfilePath(assetsRoot, activeProfile)) : null);
        var library = ExecutionImplementation.Load(assetsRoot, scope, activeImplementation);
        return (scope, library, BindingCatalog.FromLibrary(library), FunctionSemantics.FromLibrary(library), source);
    }
    private ExecutionProfile BuildDataContext()
    {
        if (document is null) throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Data.Text016"));
        CommitData(); var assets = DataAssets();
        var allowed = ProgramDataContext.Parameters(document, assets.Library, assets.Bindings, assets.Semantics, assets.Source).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        return WithRandomMetadata(ProgramDataContext.Create(document, assets.Library, assets.Bindings, assets.Semantics, assets.Scope, assets.Source,
            dataDrafts.Where(p => allowed.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)));
    }
    private ContextPreparation? AnalyzeData()
    {
        DataPreparation = null; dataGrid.ItemsSource = null;
        if (document is null) return null;
        try
        {
            CommitData(); var assets = DataAssets();
            var parameters = ProgramDataContext.Parameters(document, assets.Library, assets.Bindings, assets.Semantics, assets.Source);
            dataRows.Clear(); foreach (var parameter in parameters) dataRows.Add(new() { Parameter = parameter, Value = dataDrafts.GetValueOrDefault(parameter.Key, "") });
            var context = BuildDataContext();
            DataPreparation = UserProgramPreparation.Prepare(document, assets.Library, assets.Bindings, assets.Semantics, assets.Scope, context);
            dataGrid.ItemsSource = DataPreparation.Analysis.Variables;
            UILocalization.Set(dataNotice, "Text", () => DSNText.Concat(DSNText.Concat(DSNText.Concat((DataPreparation.Ready ? DSNText.Russian("Viewer.ExecutionPanel.Data.Text017") : DSNText.Russian("Viewer.ExecutionPanel.Data.Text018")), ""), DSNText.Russian("Viewer.ExecutionPanel.Data.Text020")), DSNText.Join("\n", DataPreparation.Diagnostics.Take(10))));
        }
        catch (Exception error) { UILocalization.Set(dataNotice, "Text", () => DSNText.Concat(DSNText.Russian("Viewer.ExecutionPanel.Data.Text021"), error.Message)); }
        return DataPreparation;
    }

    private void PrepareData()
    {
        Pause(); var preparation = AnalyzeData();
        if (document is null || preparation?.Ready != true) return;
        var assets = DataAssets(); var replacement = new ExecutionSession(document, assets.Library, assets.Bindings, preparation.Profile!);
        Session = replacement; preparedFromDSN = true;
        customScenario = false; eventRows.Clear(); stopRows.Clear(); expectationRows.Clear(); loadedSessionPath = null;
        scenario.IsChecked = false; AttachSession(); RebuildStateHistory(); Refresh();
    }
    internal void BindUser(LogicProgramDocument value)
    {
        RefreshImplementations();
        Pause(); BeginDataDocument(value); document = value; genericMode = true; activeProfile = null;
        updatingProfile = true; profileChoice.SelectedIndex = (controlProfiles ? 2 : 0); updatingProfile = false;
        Session = null; preparedFromDSN = false; Readiness = null; readinessGrid.ItemsSource = null;
        initialRows.Clear(); watchRows.Clear(); eventRows.Clear(); stopRows.Clear(); expectationRows.Clear();
        inputNames.ItemsSource = stopWords.ItemsSource = expectationWords.ItemsSource = null;
        loadedSessionPath = null; customScenario = false; selectedReference = null; UILocalization.Set(diagnostic, "Text", () => "");
        try
        {
            var assets = DataAssets(); Readiness = ProgramReadiness.Analyze(value, assets.Library, assets.Bindings);
            readinessGrid.ItemsSource = Readiness.Functions;
            PrepareData();
            if (Session is null) { UILocalization.Set(diagnostic, "Text", () => DSNText.Russian("Viewer.ExecutionPanel.Data.Text022")); tabs.SelectedIndex = 9; }
        }
        catch (Exception error) { UILocalization.Set(diagnostic, "Text", () => error.Message); tabs.SelectedIndex = 4; }
        RebuildStateHistory(); Refresh();
    }
    private void OpenDataContext()
    {
        var folder = Path.Combine(assetsRoot, "contexts");
        var dialog = UILocalization.With<global::Microsoft.Win32.OpenFileDialog>(new OpenFileDialog { Filter = DSNText.Russian("Viewer.ExecutionPanel.Data.Text023"), AddToRecent = false, CheckFileExists = true, InitialDirectory = Directory.Exists(folder) ? folder : assetsRoot }, ("Filter", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text023"))));
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) LoadDataContextFrom(dialog.FileName);
    }
    internal void LoadDataContextFrom(string path)
    {
        Pause(); if (document is null) throw new InvalidOperationException(DSNText.Russian("Viewer.ExecutionPanel.Data.Text016"));
        var assets = DataAssets(); var replacement = ProgramDataContext.Read(path, document, assets.Library, assets.Bindings, assets.Semantics, assets.Scope);
        importedDataContext = replacement; pendingRandomBatches.Clear(); dataContext.IsChecked = false; dataDrafts.Clear(); dataRows.Clear(); AnalyzeData(); RefreshRandomNotice();
    }
    private void SaveDataContext()
    {
        var folder = Path.Combine(assetsRoot, "contexts"); Directory.CreateDirectory(folder);
        var dialog = UILocalization.With<global::Microsoft.Win32.SaveFileDialog>(new SaveFileDialog { Filter = DSNText.Russian("Viewer.ExecutionPanel.Data.Text023"), AddToRecent = false, InitialDirectory = folder, FileName = "DSN-data.json" }, ("Filter", () => (object?)(DSNText.Russian("Viewer.ExecutionPanel.Data.Text023"))));
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) SaveDataContextTo(dialog.FileName);
    }
    internal void SaveDataContextTo(string path) { Pause(); ProgramDataContext.Save(path, BuildDataContext()); }

    private ProgramDataDraft? CaptureDataDraft()
    {
        CommitData();
        if (!dataDrafts.Values.Any(v => !string.IsNullOrWhiteSpace(v)) && importedDataContext is null && dataContext.IsChecked != true && pendingRandomBatches.Count == 0 && !RandomSettingsChanged()) return null;
        var assets = DataAssets();
        var source = ProgramDataContext.Create(document!, assets.Library, assets.Bindings, assets.Semantics, assets.Scope, assets.Source, new Dictionary<string, string>());
        var keys = ProgramDataContext.Parameters(document!, assets.Library, assets.Bindings, assets.Semantics, source).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        return new(WithRandomMetadata(source), dataDrafts.Where(p => keys.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)) { RandomSettings = CaptureRandomSettings() };
    }
    private void RestoreDataDraft(ProgramDataDraft? draft)
    {
        dataRows.Clear(); dataDrafts.Clear(); pendingRandomBatches.Clear(); importedDataContext = draft is not null ? ProgramDataContext.Clone(draft.Context) : Session?.Profile.PreparedDSNSha256 is not null ? ProgramDataContext.Clone(Session.Profile) : null; dataContext.IsChecked = false;
        if (draft is not null) foreach (var (key, value) in draft.Values) dataDrafts[key] = value;
        RestoreRandomSettings(draft?.RandomSettings); AnalyzeData(); RefreshRandomNotice();
    }
}
