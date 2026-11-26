// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Security.Cryptography;
using System.Text.Json;
using DSN.Core;

namespace DSN.Execution;

public sealed record ImplementationRun(string Implementation, ScenarioIdentity Identity, string StateHistorySha256, int CheckedExpectations);
public sealed record ImplementationDifference(long Tick, string Word, string Implementation, ushort Expected, ushort Actual);
public sealed record ImplementationComparisonResult(string Format, int Version, string Result, string DSNSha256, string WorkloadSha256,
    string SourceScenarioSha256, int CompletedTicks, int Words, long ComparedValues, bool CompatibilityConfirmed,
    IReadOnlyList<ImplementationRun> Runs, ImplementationDifference? Difference);

public static class ImplementationComparison
{
    public static ExecutionScenario ForImplementation(ExecutionScenario source, ExecutionSession original, ExecutionSession target)
    {
        // Перепривязка разрешена только явно: исходная идентичность сначала проверяется обычным валидатором.
        var validated = ScenarioFile.Validate(source, original);
        if (original.Identity.DSNSha256 != target.Identity.DSNSha256 || original.Identity.ProfileSha256 != target.Identity.ProfileSha256 ||
            original.Identity.BindingsSha256 != target.Identity.BindingsSha256 ||
            original.Identity.CompatibilityConfirmed != target.Identity.CompatibilityConfirmed)
            throw new InvalidDataException(DSNText.Russian("Execution.ImplementationComparison.Text001"));
        var copy = ExecutionJson.Parse<ExecutionScenario>(JsonSerializer.Serialize(validated, DSNProgramBinder.JsonOptions), ScenarioFile.MaxBytes);
        copy.Identity = target.Identity;
        return ScenarioFile.Validate(copy, target);
    }

    public static ImplementationComparisonResult Run(string dsnPath, string assetsRoot, string? profileName, string? scenarioPath = null)
    {
        var document = DSNReader.Read(dsnPath);
        var supplied = scenarioPath is null ? null : ScenarioFile.Read(scenarioPath);
        var context = profileName is null ? supplied?.PreparedProfile ?? throw new InvalidDataException(DSNText.Russian("Profile.Required")) : null;
        var scope = context ?? DSNProgramBinder.ReadJson<ExecutionProfile>(ExecutionSession.ProfilePath(assetsRoot, profileName!));
        var ids = ExecutionImplementation.Available(assetsRoot, scope);
        if (ids.Length == 0) throw new InvalidOperationException(DSNText.Russian("Implementation.Connect"));
        var sessions = ids.Select(id => context is null ? ExecutionSession.FromAssets(document, assetsRoot, profileName!, id)
            : new ExecutionSession(document, ExecutionImplementation.Load(assetsRoot, context, id), ProgramDataContext.Clone(context))).ToArray();
        var original = sessions[0];
        var scenario = scenarioPath is null ? new ExecutionScenario { Identity = original.Identity, Steps = original.Profile.Steps,
            Title = original.Profile.Description, Events = original.Profile.Events, Observe = original.Profile.Observe } : supplied!;
        if (supplied is not null)
        {
            var source = sessions.SingleOrDefault(s => s.Identity == scenario.Identity)
                ?? throw new InvalidDataException(DSNText.Russian("Implementation.Record"));
            scenario = ForImplementation(scenario, source, original);
        }
        scenario = ScenarioFile.Validate(scenario, original);
        foreach (var target in sessions.Skip(1)) ForImplementation(scenario, original, target);
        var workloadHash = ScenarioFile.Fingerprint(new { original.Identity.DSNSha256, original.Identity.ProfileSha256,
            original.Identity.BindingsSha256, scenario.Steps, scenario.InitialOverrides, scenario.Events, scenario.Expectations });
        var words = original.Bound.Program.Addresses.Keys.Order(StringComparer.Ordinal).ToArray();
        foreach (var target in sessions.Skip(1))
            if (!words.SequenceEqual(target.Bound.Program.Addresses.Keys.Order(StringComparer.Ordinal))) throw new InvalidDataException(DSNText.Russian("Execution.ImplementationComparison.Text002"));
        // Сравнение потоковое: большие опыты не требуют трёх полных историй в памяти.
        var machines = sessions.Select(s => s.Bound.Program.CreateMachine()).ToArray();
        foreach (var machine in machines) foreach (var (name, value) in scenario.InitialOverrides) machine.WriteWord(name, value);
        var hashes = ids.Select(_ => IncrementalHash.CreateHash(HashAlgorithmName.SHA256)).ToArray();
        var checks = new int[ids.Length]; var events = scenario.Events.ToDictionary(e => e.Tick);
        var expected = (scenario.Expectations ?? []).GroupBy(e => e.Tick).ToDictionary(g => g.Key, g => g.ToArray());
        ImplementationDifference? difference = null; long compared = 0; var completed = 0;
        try
        {
            for (var tick = 0; tick <= scenario.Steps; tick++)
            {
                if (tick > 0) foreach (var machine in machines) machine.Step(events.TryGetValue(tick, out var input) ? input.Values : null);
                for (var i = 0; i < machines.Length; i++)
                {
                    var values = new byte[words.Length * 2];
                    for (var w = 0; w < words.Length; w++)
                    {
                        var value = machines[i].ReadWord(words[w]); values[w * 2] = (byte)value; values[w * 2 + 1] = (byte)(value >> 8);
                        if (i > 0) { compared++; var reference = machines[0].ReadWord(words[w]); if (value != reference) difference ??= new(tick, words[w], ids[i], reference, value); }
                    }
                    hashes[i].AppendData(values);
                    foreach (var point in expected.GetValueOrDefault(tick) ?? [])
                    {
                        checks[i]++; var value = machines[i].ReadWord(point.Word);
                        if ((value & (point.Mask ?? ushort.MaxValue)) != point.Value) difference ??= new(tick, point.Word, ids[i], point.Value, value);
                    }
                }
                completed = tick; if (difference is not null) break;
            }
            var runs = ids.Select((id, i) => new ImplementationRun(id, sessions[i].Identity, Convert.ToHexString(hashes[i].GetHashAndReset()), checks[i])).ToArray();
            return new("DSNCompiler.ImplementationComparison", 1, difference is null ? "PASS" : "FAIL", document.SourceSha256,
                workloadHash, ScenarioFile.Fingerprint(scenario), completed, words.Length, compared,
                original.Profile.CompatibilityConfirmed, runs, difference);
        }
        finally { foreach (var hash in hashes) hash.Dispose(); }
    }
}
