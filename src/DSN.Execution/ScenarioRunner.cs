// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using DSN.Core;

namespace DSN.Execution;

public sealed record StateSample(long Tick, IReadOnlyDictionary<string, ushort> Words, AdspFlags Flags);
public sealed record ScenarioResult(string Result, string Profile, bool CompatibilityConfirmed, string Description,
    IReadOnlyList<string> Assumptions, string TimeUnit, string DSNSha256, string IncSha256, string ProfileSha256,
    string BindingsSha256, string Kernel, IReadOnlyList<string> Order, IReadOnlyList<MacroInvocation> Invocations,
    IReadOnlyList<StateSample> Samples);
public sealed record SavedScenarioResult(string Result, bool CompatibilityConfirmed,
    string TimeUnit, string ScenarioSha256, ExecutionScenario Scenario, IReadOnlyList<string> Order, IReadOnlyList<StateSample> Samples)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExpectationReport? Verification { get; init; }
}
public sealed record SessionContinuationResult(string Result, ScenarioIdentity Identity, long ResumedAt, long Tick,
    int TickLimit, string TimeUnit, bool UseScenarioEvents, string? ScenarioSha256, AdspMachineState Machine,
    string MemorySha256, IReadOnlyList<InputEvent> ManualInputs, IReadOnlyList<StateSample> Samples)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public StopHit? Stop { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExpectationReport? Verification { get; init; }
}

public static class ScenarioRunner
{
    public static SessionContinuationResult Resume(string dsnPath, string assetsRoot, string? profileName, string sessionPath, int steps, string? implementation = null)
    {
        var saved = SessionFile.Read(sessionPath); var document = DSNReader.Read(dsnPath);
        var profile = profileName is null ? saved.PreparedProfile ?? throw new InvalidDataException(DSNText.Russian("Profile.Required"))
            : DSNProgramBinder.ReadJson<ExecutionProfile>(ExecutionSession.ProfilePath(assetsRoot, profileName));
        var selected = ExecutionImplementation.ForIdentity(assetsRoot, profile, saved.Identity, implementation);
        var session = profileName is null ? PreparedProgramContext.Create(document, assetsRoot, profile, saved.Identity, selected)
            : ExecutionSession.FromAssets(document, assetsRoot, profileName, selected);
        session.RestoreSession(saved); var start = session.Tick;
        session.AdvanceUntilStop(steps); var state = session.CaptureSession().Machine;
        var verification = session.VerifyExpectations();
        return new(verification.Total == 0 ? "PASS" : verification.Result, session.Identity, start, session.Tick, session.TickLimit, DSNText.Russian("Execution.ScenarioRunner.Text001"), session.UseScenarioEvents,
            session.ScenarioSha256, state, ScenarioFile.Fingerprint(state.Memory), session.ManualInputs, session.Samples)
            { Stop = session.LastStop, Verification = verification.Total == 0 ? null : verification };
    }

    public static SavedScenarioResult RunSaved(string dsnPath, string assetsRoot, string? profileName, string scenarioPath, string? implementation = null)
    {
        var source = ScenarioFile.Read(scenarioPath); var document = DSNReader.Read(dsnPath);
        var profile = profileName is null ? source.PreparedProfile ?? throw new InvalidDataException(DSNText.Russian("Profile.Required"))
            : DSNProgramBinder.ReadJson<ExecutionProfile>(ExecutionSession.ProfilePath(assetsRoot, profileName));
        var selected = ExecutionImplementation.ForIdentity(assetsRoot, profile, source.Identity, implementation);
        var session = profileName is null ? PreparedProgramContext.Create(document, assetsRoot, profile, source.Identity, selected)
            : ExecutionSession.FromAssets(document, assetsRoot, profileName, selected);
        var scenario = ScenarioFile.Validate(source, session);
        session.ApplyScenario(scenario); session.Step(scenario.Steps);
        var verification = session.VerifyExpectations();
        return new(verification.Failed > 0 ? "FAIL" : "PASS", session.Profile.CompatibilityConfirmed, DSNText.Russian("Execution.ScenarioRunner.Text001"),
            session.ScenarioSha256!, scenario, session.Bound.Program.InstanceOrder, session.Samples) { Verification = verification.Total == 0 ? null : verification };
    }

    public static ScenarioResult Run(string dsnPath, string incPath, string profilePath, IFunctionLibrary? source = null)
    {
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var dsnHash = Hash(dsnPath); var profileHash = Hash(profilePath);
        var profile = DSNProgramBinder.ReadJson<ExecutionProfile>(profilePath);
        if (profile.Steps is < 1 or > 10_000) throw new InvalidDataException(DSNText.Russian("Execution.ScenarioRunner.Text003"));
        if (profile.Events.Any(e => e.Tick < 1 || e.Tick > profile.Steps || e.Values.Keys.Any(name => !profile.InputWords.Contains(name, StringComparer.OrdinalIgnoreCase))) ||
            profile.Events.Select(e => e.Tick).Distinct().Count() != profile.Events.Length)
            throw new InvalidDataException(DSNText.Russian("Execution.ScenarioRunner.Text004"));
        var sources = new[] { incPath }.Concat(profile.Libraries.Skip(1).Select(n => Path.Combine(Path.GetDirectoryName(incPath)!, ExecutionSession.SafeName(n)))).ToArray();
        var library = source ?? AdspLibrary.ReadMany(sources);
        var bindings = BindingCatalog.FromLibrary(library);
        var bindingsHash = ScenarioFile.Fingerprint(bindings);
        var bound = DSNProgramBinder.Bind(DSNReader.Read(dsnPath), library, bindings, profile);
        var machine = bound.Program.CreateMachine();
        var samples = new List<StateSample>();
        void Capture() => samples.Add(new(machine.Tick, profile.Observe.ToDictionary(name => name, machine.ReadWord, StringComparer.OrdinalIgnoreCase), machine.Flags));
        Capture();
        var events = profile.Events.ToDictionary(e => e.Tick);
        for (var tick = 1; tick <= profile.Steps; tick++)
        {
            machine.Step(events.TryGetValue(tick, out var input) ? input.Values : null);
            Capture();
        }
        return new("PASS", profile.Id, profile.CompatibilityConfirmed, profile.Description, profile.Assumptions,
            DSNText.Russian("Execution.ScenarioRunner.Text001"), dsnHash, library.Sha256, profileHash, bindingsHash, bound.Program.KernelId,
            bound.Program.InstanceOrder, bound.Invocations, samples);
    }
}
