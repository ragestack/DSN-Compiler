// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Globalization;
using DSN.Core;

namespace DSN.Execution;

public sealed partial class ExecutionSession
{
    public const int MaxTicks = 10_000;
    private readonly List<StateSample> samples = [];
    private readonly Dictionary<string, ushort> pending = new(StringComparer.OrdinalIgnoreCase);
    private IExecutionMachine machine;
    private Dictionary<int, InputEvent> activeEvents = [];
    private readonly List<InputEvent> manualInputs = [];
    private Dictionary<string, ushort> appliedInitial = [];
    private ExecutionScenario? appliedScenario;
    public LogicProgramDocument Document { get; }
    public ExecutionProfile Profile { get; }
    public BoundDSNProgram Bound { get; }
    public IReadOnlyList<string> Variables { get; }
    public IReadOnlyList<StateSample> Samples => samples.AsReadOnly();
    public IReadOnlyDictionary<string, ushort> PendingInputs => new System.Collections.ObjectModel.ReadOnlyDictionary<string, ushort>(pending);
    public IReadOnlySet<string> Changed { get; private set; } = new HashSet<string>();
    public StepExecutionRecord? LastStep { get; private set; }
    public long Tick => machine.Tick;
    public bool Faulted => machine.Faulted;
    public string? FaultDiagnostic { get; private set; }
    public AdspFlags Flags => machine.Flags;
    public IReadOnlyDictionary<string, uint> Registers => machine.Registers;
    public string Architecture => Bound.Program.Architecture;
    public ExecutionSchedule Schedule => Bound.Schedule;
    public bool UseScenarioEvents { get; private set; }
    public string LibrarySha256 => Bound.Program.LibrarySha256;
    public ScenarioIdentity Identity { get; }
    public string? ScenarioTitle { get; private set; }
    public string? ScenarioSha256 { get; private set; }
    public int TickLimit { get; private set; } = MaxTicks;
    public IReadOnlyList<InputEvent> ManualInputs => manualInputs.Select(e => new InputEvent { Tick = e.Tick, Values = new(e.Values) }).ToArray();

    public static ExecutionSession FromAssets(LogicProgramDocument document, string root, string profileName, string? implementation = null)
    {
        var path = ProfilePath(root, profileName);
        var profile = DSNProgramBinder.ReadJson<ExecutionProfile>(path);
        return new(document, ExecutionImplementation.Load(root, profile, implementation), profile);
    }
    public static string SafeName(string name) => name.Length > 0 && name == Path.GetFileName(name) && !name.Contains(':') && name is not ("." or "..")
        ? name : throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Text002"));
    public static string ProfilePath(string root, string name) => Path.IsPathRooted(name)
        ? Path.GetFullPath(name) : Path.Combine(root, "profiles", SafeName(name));

    public ExecutionSession(LogicProgramDocument document, string incPath, string profilePath, IFunctionLibrary? source = null)
        : this(document, source ?? AdspLibrary.Read(incPath), DSNProgramBinder.ReadJson<ExecutionProfile>(profilePath)) { }

    public ExecutionSession(LogicProgramDocument document, IFunctionLibrary library, ExecutionProfile profile)
        : this(document, library, BindingCatalog.FromLibrary(library), profile) { }

    public ExecutionSession(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog bindings, ExecutionProfile profile)
    {
        Document = document;
        Profile = profile;
        if (library.Metadata is not null && profile.Bindings != "INC") throw new InvalidDataException(DSNText.Russian("Profile.InvalidContract"));
        if (Profile.Events.Any(e => e.Tick < 1 || e.Tick > MaxTicks || e.Values.Keys.Any(n => !Profile.InputWords.Contains(n, StringComparer.OrdinalIgnoreCase))) ||
            Profile.Events.Select(e => e.Tick).Distinct().Count() != Profile.Events.Length)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Text004"));
        Bound = DSNProgramBinder.Bind(document, library, bindings, Profile);
        Identity = new(document.SourceSha256, Bound.Program.LibrarySha256, Profile.Id, ScenarioFile.Fingerprint(Profile),
            ScenarioFile.Fingerprint(bindings), Bound.Program.KernelId, Profile.CompatibilityConfirmed);
        Variables = Bound.Program.Addresses.Keys.Where(n => !n.StartsWith("__WORD_", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        machine = Bound.Program.CreateMachine();
        Capture();
    }

    public ushort ReadWord(string name) => machine.ReadWord(name);
    public int ReadValue(ValueBinding valueBinding) => valueBinding.Word is { } word ? machine.ReadWord(word) : valueBinding.Constant!.Value;
    public bool ValueChanged(ValueBinding valueBinding) => valueBinding.Word is { } word && Changed.Contains(word);
    public IReadOnlyDictionary<string, ushort> InitialValues() => Bound.Program.CreateMachine().Snapshot();

    public void QueueInput(string name, ushort value)
    {
        if (!Profile.InputWords.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.ExecutionSession.Text005"), name));
        if (Faulted) throw new InvalidOperationException(DSNText.Russian("Execution.AdspProgram.Text046"));
        pending[name] = value;
    }

    public void Reset(bool useScenarioEvents, IReadOnlyDictionary<string, ushort>? initialOverrides = null)
    {
        ValidateInitialOverrides(initialOverrides);
        var replacement = Bound.Program.CreateMachine();
        foreach (var (name, value) in initialOverrides ?? new Dictionary<string, ushort>()) replacement.WriteWord(name, value);
        machine = replacement; UseScenarioEvents = useScenarioEvents; FaultDiagnostic = null;
        appliedInitial = new(initialOverrides ?? new Dictionary<string, ushort>(), StringComparer.OrdinalIgnoreCase); appliedScenario = null;
        activeEvents = useScenarioEvents ? Profile.Events.ToDictionary(e => e.Tick, e => new InputEvent { Tick = e.Tick, Values = new(e.Values, StringComparer.OrdinalIgnoreCase) }) : [];
        TickLimit = MaxTicks; ScenarioTitle = ScenarioSha256 = null;
        pending.Clear(); manualInputs.Clear(); samples.Clear(); processorRows.Clear(); processorFirstTick = 0;
        LastStep = null; LastStop = null; Changed = new HashSet<string>(); Capture();
    }

    internal void ValidateInitialOverrides(IReadOnlyDictionary<string, ushort>? initialOverrides)
    {
        var written = new Dictionary<int, ushort>();
        foreach (var (name, value) in initialOverrides ?? new Dictionary<string, ushort>())
        {
            if (!Variables.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.ExecutionSession.Text006"), name));
            var address = Bound.Program.Addresses[name];
            if (written.TryGetValue(address, out var previous) && previous != value) throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.ExecutionSession.Text007"), name));
            written[address] = value;
        }
    }

    public void ApplyScenario(ExecutionScenario scenario)
    {
        var validated = ScenarioFile.Validate(scenario, this);
        Reset(true, validated.InitialOverrides);
        activeEvents = validated.Events.ToDictionary(e => e.Tick);
        appliedScenario = validated;
        TickLimit = validated.Steps; ScenarioTitle = validated.Title; ScenarioSha256 = ScenarioFile.Fingerprint(validated);
    }

    public void Step(int count = 1)
    {
        if (count < 1 || Tick + count > TickLimit) throw new InvalidOperationException(DSNText.Russian("Execution.ExecutionSession.Text008", TickLimit));
        LastStop = null;
        for (var i = 0; i < count; i++)
        {
            var before = machine.Snapshot();
            var inputs = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            if (UseScenarioEvents && activeEvents.TryGetValue((int)Tick + 1, out var entry))
                foreach (var (name, value) in entry.Values) inputs[name] = value;
            foreach (var (name, value) in pending) inputs[name] = value;
            try { LastStep = machine.Step(inputs); }
            catch (Exception error) { if (machine.Faulted) FaultDiagnostic = DSNText.Translate(error.Message); throw; }
            if (pending.Count > 0) manualInputs.Add(new() { Tick = (int)machine.Tick, Values = new(pending, StringComparer.OrdinalIgnoreCase) });
            pending.Clear();
            Changed = machine.Snapshot().Where(p => before[p.Key] != p.Value).Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Capture();
        }
    }

    private void Capture()
    {
        samples.Add(new(Tick, Variables.ToDictionary(n => n, machine.ReadWord, StringComparer.OrdinalIgnoreCase), machine.Flags));
        var registers = machine.Registers;
        processorRows.Add(Bound.Program.RegisterNames.Select(n => registers[n]).ToArray());
    }
    public static string FormatWord(int value) => value is 0 or 1 ? value.ToString(CultureInfo.InvariantCulture) : DSNText.Compose($"{value} (0x{value:X4})");
    public static ushort ParseWord(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ushort.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)) return hex;
        if (ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return value;
        throw new FormatException(DSNText.Russian("Execution.ExecutionSession.Text009"));
    }
}
