// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Globalization;
using System.Text.Json;

namespace DSN.Execution;

public sealed partial class ExecutionSession
{
    private string LayoutSha256 => ScenarioFile.Fingerprint(new { MemoryWords = Bound.Program.InitialImage.Length,
        Registers = Bound.Program.RegisterNames, Addresses = Bound.Program.Addresses.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray(),
        Order = Bound.Program.InstanceOrder, Variables });

    public SavedExecutionSession CaptureSession(SessionViewState? view = null)
    {
        if (Identity.DSNSha256.Length != 64) throw new InvalidOperationException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text001"));
        var state = machine.CaptureState();
        var result = new SavedExecutionSession { ProcessorHistory = CaptureProcessorHistory(), PreparedProfile = PreparedProgramContext.Capture(this), Stops = CaptureStops(), Identity = Identity, LayoutSha256 = LayoutSha256, Machine = state,
            UseScenarioEvents = UseScenarioEvents, InitialOverrides = new(appliedInitial),
            Scenario = appliedScenario is null ? null : ScenarioFile.Validate(appliedScenario, this), PendingInputs = new(pending),
            ManualInputs = ManualInputs.ToArray(), HistoryWords = Variables.ToArray(), HistoryCount = samples.Count,
            History = samples.Select(s => new SavedHistoryRow(Variables.Select(n => s.Words[n]).ToArray(), s.Flags)).ToArray(),
            LastStep = CopyExecutionRecord(LastStep), Changed = Changed.Order(StringComparer.Ordinal).ToArray(), View = CopyView(view ?? DefaultView(), Tick) };
        return result;
    }

    public SessionViewState RestoreSession(SavedExecutionSession saved)
    {
        var replacement = PrepareRestore(saved);
        machine = replacement.Machine; UseScenarioEvents = saved.UseScenarioEvents; FaultDiagnostic = null;
        appliedInitial = replacement.Initial; appliedScenario = replacement.Scenario;
        activeEvents = (appliedScenario?.Events ?? (UseScenarioEvents ? Profile.Events : [])).ToDictionary(e => e.Tick,
            e => new InputEvent { Tick = e.Tick, Values = new(e.Values, StringComparer.OrdinalIgnoreCase) });
        TickLimit = appliedScenario?.Steps ?? MaxTicks; ScenarioTitle = appliedScenario?.Title;
        ScenarioSha256 = appliedScenario is null ? null : ScenarioFile.Fingerprint(appliedScenario);
        pending.Clear(); foreach (var (name, value) in replacement.Pending) pending.Add(name, value);
        manualInputs.Clear(); manualInputs.AddRange(replacement.Manual);
        samples.Clear(); samples.AddRange(replacement.History);
        processorRows.Clear(); processorRows.AddRange(replacement.Processor.Values);
        processorFirstTick = replacement.Processor.FirstTick;
        LastStep = replacement.LastStep; Changed = replacement.Changed;
        stopConditions = replacement.Stops?.Conditions ?? []; LastStop = replacement.Stops?.LastHit;
        return replacement.View;
    }

    private sealed record Restored(IExecutionMachine Machine, Dictionary<string, ushort> Initial, ExecutionScenario? Scenario,
        Dictionary<string, ushort> Pending, InputEvent[] Manual, StateSample[] History, StepExecutionRecord? LastStep, HashSet<string> Changed, SessionViewState View, ExecutionStopState? Stops, ProcessorHistory Processor);

    private Restored PrepareRestore(SavedExecutionSession saved)
    {
        if (saved is null || saved.Identity != Identity || saved.LayoutSha256 != LayoutSha256)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text002"));
        PreparedProgramContext.Validate(saved.PreparedProfile, Identity);
        if (saved.Machine is null || saved.InitialOverrides is null || saved.PendingInputs is null || saved.ManualInputs is null ||
            saved.HistoryWords is null || saved.History is null || saved.Changed is null || saved.View is null)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text003"));
        var scenario = saved.Scenario is null ? null : ScenarioFile.Validate(saved.Scenario, this);
        var limit = scenario?.Steps ?? MaxTicks;
        if (saved.Machine.Tick < 0 || saved.Machine.Tick > limit || saved.HistoryCount != saved.Machine.Tick + 1 ||
            saved.History.Length != saved.HistoryCount || !saved.HistoryWords.SequenceEqual(Variables) ||
            (long)saved.HistoryCount * Variables.Count > SessionFile.MaxHistoryCells || scenario is not null && !saved.UseScenarioEvents)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text004"));
        var initial = new Dictionary<string, ushort>(saved.InitialOverrides, StringComparer.OrdinalIgnoreCase);
        ValidateInitialOverrides(initial);
        if (scenario is not null && !initial.OrderBy(p => p.Key).SequenceEqual(scenario.InitialOverrides.OrderBy(p => p.Key)))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text005"));
        var restored = Bound.Program.CreateMachine(); restored.RestoreState(saved.Machine);
        var initialMachine = Bound.Program.CreateMachine(); foreach (var (name, value) in initial) initialMachine.WriteWord(name, value);
        var history = new StateSample[saved.HistoryCount];
        var historyNames = Variables.ToArray();
        var historyIndex = historyNames.Select((n, i) => KeyValuePair.Create(n, i)).ToDictionary(StringComparer.OrdinalIgnoreCase);
        var aliasColumns = Variables.Select((name, i) => (Address: Bound.Program.Addresses[name], Column: i)).GroupBy(p => p.Address)
            .Where(g => g.Count() > 1).Select(g => g.Select(p => p.Column).ToArray()).ToArray();
        for (var i = 0; i < history.Length; i++)
        {
            var row = saved.History[i];
            if (row is null || row.Values is null || row.Values.Length != Variables.Count || row.Flags is null)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text006"));
            foreach (var columns in aliasColumns)
                if (columns.Any(c => row.Values[c] != row.Values[columns[0]])) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text007"));
            history[i] = new(i, new SavedWords(historyNames, historyIndex, (ushort[])row.Values.Clone()), row.Flags);
        }
        if (history[0].Flags != initialMachine.Flags || Variables.Any(n => history[0].Words[n] != initialMachine.ReadWord(n)) ||
            history[^1].Flags != restored.Flags || Variables.Any(n => history[^1].Words[n] != restored.ReadWord(n)))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text008"));
        if (restored.Tick == 0 && !saved.Machine.Memory.SequenceEqual(initialMachine.CaptureState().Memory))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text009"));
        var pendingInputs = InputMap(saved.PendingInputs);
        var ticks = new HashSet<int>(); var assignmentCount = 0;
        var manual = saved.ManualInputs.Select(entry =>
        {
            if (entry is null || entry.Tick < 1 || entry.Tick > restored.Tick || !ticks.Add(entry.Tick) || entry.Values is null || entry.Values.Count == 0)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text010"));
            assignmentCount += entry.Values.Count;
            if (assignmentCount > SessionFile.MaxManualAssignments) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text011"));
            return new InputEvent { Tick = entry.Tick, Values = InputMap(entry.Values) };
        }).ToArray();
        if (!manual.Select(e => e.Tick).SequenceEqual(manual.Select(e => e.Tick).Order())) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text012"));
        var last = CopyExecutionRecord(saved.LastStep);
        if (restored.Tick == 0 ? last is not null : last is null || last.Tick != restored.Tick || last.Flags != restored.Flags)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text013"));
        if (last is not null)
        {
            if (last.Blocks is null || last.Blocks.Count != Bound.Program.BlockInfo.Count || last.Blocks.Sum(b => (long)b.Instructions) > 100_000)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text014"));
            for (var i = 0; i < last.Blocks.Count; i++)
            {
                var block = last.Blocks[i]; var expected = Bound.Program.BlockInfo[i];
                if (block.Instance != expected.Instance || block.Macro != expected.Macro || block.Instructions < 0 || block.Changes.Count > block.Instructions ||
                    block.Changes.Any(c => c is null || !Bound.Program.Addresses.ContainsKey(c.Name)))
                    throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text015"));
            }
        }
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in saved.Changed)
            if (name is null || !Bound.Program.Addresses.ContainsKey(name) || !changed.Add(name)) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text016"));
        if (restored.Tick == 0 && changed.Count > 0 || restored.Tick > 0 && Variables.Any(n => changed.Contains(n) != (history[^1].Words[n] != history[^2].Words[n])))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text017"));
        var stops = PrepareStops(saved.Stops, history);
        var processor = PrepareProcessorRestore(saved, initialMachine);
        return new(restored, initial, scenario, pendingInputs, manual, history, last, changed, CopyView(saved.View, restored.Tick), stops, processor);
    }

    private Dictionary<string, ushort> InputMap(IReadOnlyDictionary<string, ushort> source)
    {
        var result = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in source)
            if (!Profile.InputWords.Contains(name, StringComparer.OrdinalIgnoreCase) || !result.TryAdd(name, value))
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionSession.Persistence.Text018"), name));
        return result;
    }

    private sealed class SavedWords(string[] names, Dictionary<string, int> index, ushort[] values) : IReadOnlyDictionary<string, ushort>
    {
        public ushort this[string key] => values[index[key]];
        public IEnumerable<string> Keys => names.Select(n => n);
        public IEnumerable<ushort> Values => values.Select(v => v);
        public int Count => names.Length;
        public bool ContainsKey(string key) => index.ContainsKey(key);
        public bool TryGetValue(string key, out ushort value) { var found = index.TryGetValue(key, out var i); value = found ? values[i] : (ushort)0; return found; }
        public IEnumerator<KeyValuePair<string, ushort>> GetEnumerator() => names.Select((n, i) => KeyValuePair.Create(n, values[i])).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static StepExecutionRecord? CopyExecutionRecord(StepExecutionRecord? history)
    {
        if (history is null) return null;
        if (history.Flags is null || history.Blocks is null || history.Blocks.Any(b => b is null || b.Changes is null || b.Changes.Any(c => c is null)))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text019"));
        return new(history.Tick, history.Blocks.Select(b => new FunctionExecutionRecord(b.Instance, b.Macro, b.Instructions, b.Changes.ToArray())).ToArray(), history.Flags);
    }

    private SessionViewState DefaultView()
    {
        var defaults = InitialValues(); var source = appliedScenario;
        var input = Profile.InputWords.FirstOrDefault() ?? "";
        return new() { Observed = source?.Observe.ToArray() ?? Profile.Observe.ToArray(),
            ExpectationDrafts = source?.Expectations?.Select(e => new ExpectationDraft(e.Tick.ToString(CultureInfo.InvariantCulture), e.Word,
                DSNText.Concat("0x", e.Value.ToString("X4")), e.Mask is { } mask ? DSNText.Concat("0x", mask.ToString("X4")) : "")).ToArray(),
            Initial = Variables.Select(n => new InitialWordDraft(n, appliedInitial.ContainsKey(n), DSNText.Concat("0x", appliedInitial.GetValueOrDefault(n, defaults[n]).ToString("X4")))).ToArray(),
            ScenarioTitle = source?.Title ?? DSNText.Concat(DSNText.Russian("Execution.ExecutionSession.Persistence.Text020"), Profile.Id), ScenarioSteps = (source?.Steps ?? Math.Clamp(Profile.Steps, 1, MaxTicks)).ToString(CultureInfo.InvariantCulture),
            Events = (source?.Events ?? Profile.Events).SelectMany(e => e.Values.Select(p => new InputEventDraft(e.Tick.ToString(CultureInfo.InvariantCulture), p.Key, p.Value.ToString(CultureInfo.InvariantCulture)))).ToArray(),
            CustomScenario = source is not null, EventsOnReset = UseScenarioEvents, SelectedInput = input,
            InputText = input.Length == 0 ? "0" : DSNText.Concat("0x", pending.GetValueOrDefault(input, ReadWord(input)).ToString("X4")), SelectedTab = 6 };
    }

    private SessionViewState CopyView(SessionViewState view, long tick)
    {
        static bool Text(string? value, int max = 256) => value is not null && value.Length <= max;
        if (view is null || view.Observed is null || view.Initial is null || view.Events is null || !Text(view.ScenarioTitle, 1000) ||
            !Text(view.ScenarioSteps, 64) || !Text(view.InputText) || view.SelectedTab is < 0 or > 9 ||
            view.Initial.Length != Variables.Count || view.Events.Length > ScenarioFile.MaxAssignments ||
            view.Observed.Distinct(StringComparer.OrdinalIgnoreCase).Count() != view.Observed.Length || view.Observed.Any(n => !Variables.Contains(n, StringComparer.OrdinalIgnoreCase)) ||
            view.SelectedBlock is not null && !Document.Pages.SelectMany(p => p.Instances).Any(i => i.Reference == view.SelectedBlock) ||
            (view.SelectedInput != "" || Profile.InputWords.Length != 0) && !Profile.InputWords.Contains(view.SelectedInput, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text021"));
        if (view.Timeline is { } chart && (chart.HistoryTab is < 0 or > 1 || chart.StartTick < 0 || chart.StartTick > tick ||
            chart.CursorTick < 0 || chart.CursorTick > tick || chart.VisibleTicks is < 1 or > MaxTicks + 1))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text022"));
        if (view.StopDrafts is { } drafts && (drafts.Length > MaxStopConditions || drafts.Any(r => r is null ||
            !Text(r.Value) || r.Word is null || r.Word != "" && !Variables.Contains(r.Word, StringComparer.OrdinalIgnoreCase) || !Enum.IsDefined(r.Comparison))))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text023"));
        if (view.ExpectationDrafts is { } expected && (expected.Length > ScenarioFile.MaxExpectations || expected.Any(r => r is null ||
            !Text(r.Tick, 64) || !Text(r.Value) || !Text(r.Mask) || r.Word is null || r.Word != "" && !Variables.Contains(r.Word, StringComparer.OrdinalIgnoreCase))))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text024"));
        if (view.DataDraft is { } data)
        {
            RandomTestData.Validate(data.RandomSettings);
            ProgramDataContext.Shape(data.Context);
            _ = ProgramDataContext.Clone(data.Context);
            if (data.Context.PreparedDSNSha256 != Identity.DSNSha256 || data.Context.VerifiedLibraries?.Contains(Identity.LibrarySha256, StringComparer.Ordinal) != true ||
                !data.Context.Libraries.SequenceEqual(Profile.Libraries) || data.Values is null || data.Values.Count > Bound.Ports.Count + Variables.Count ||
                data.Values.Any(p => !Text(p.Value, 128) || p.Key is null || p.Key.Length > 512 ||
                    !(p.Key.StartsWith("constant:", StringComparison.Ordinal) && Bound.Ports.TryGetValue(p.Key[9..], out var port) && port.Constant.HasValue ||
                      p.Key.StartsWith("environment:", StringComparison.Ordinal) && Bound.Program.Addresses.ContainsKey(p.Key[12..]))) ||
                data.Context.PortConstants?.Any(p => !Bound.Ports.TryGetValue(p.Key, out var port) || !port.Constant.HasValue) == true)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text025"));
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in view.Initial)
            if (row is null || !Variables.Contains(row.Name, StringComparer.OrdinalIgnoreCase) || !names.Add(row.Name) || !Text(row.Value))
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text026"));
        foreach (var row in view.Events)
            if (row is null || !Text(row.Tick, 64) || !Text(row.Value) || row.Word is null || row.Word.Length > 256 ||
                row.Word != "" && !Profile.InputWords.Contains(row.Word, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Persistence.Text027"));
        return ExecutionJson.Parse<SessionViewState>(JsonSerializer.Serialize(view, DSNProgramBinder.JsonOptions), SessionFile.MaxMetadataBytes);
    }
}
