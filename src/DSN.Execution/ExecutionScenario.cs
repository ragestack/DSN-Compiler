// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace DSN.Execution;

public sealed record ScenarioIdentity(string DSNSha256, string LibrarySha256, string ProfileId,
    string ProfileSha256, string BindingsSha256, string Kernel, bool CompatibilityConfirmed);

public sealed class ExecutionScenario
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExecutionProfile? PreparedProfile { get; set; }
    [JsonRequired] public string Format { get; set; } = "DSNCompiler.Scenario";
    [JsonRequired] public int Version { get; set; } = 2;
    public string Title { get; set; } = DSNText.Russian("Execution.ExecutionScenario.Text001");
    [JsonRequired] public ScenarioIdentity Identity { get; set; } = null!;
    [JsonRequired] public int Steps { get; set; }
    public Dictionary<string, ushort> InitialOverrides { get; set; } = [];
    public InputEvent[] Events { get; set; } = [];
    public string[] Observe { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ScenarioExpectation[]? Expectations { get; set; }
}

public static class ScenarioFile
{
    public const int MaxAssignments = 10_000;
    public const int MaxExpectations = 10_000;
    public const int MaxBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions ReadableJson = new(DSNProgramBinder.JsonOptions)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic)
    };
    public static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, DSNProgramBinder.JsonOptions))));

    public static ExecutionScenario Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaxBytes) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text002"));
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return Parse(reader.ReadToEnd());
    }

    public static ExecutionScenario Parse(string json)
    {
        var scenario = ExecutionJson.Parse<ExecutionScenario>(json, MaxBytes);
        if (scenario.Format != "DSNCompiler.Scenario" || scenario.Version != 2)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text003"));
        return scenario;
    }

    public static void Save(string path, ExecutionScenario scenario, ExecutionSession session)
    {
        var validated = Validate(scenario, session);
        var json = JsonSerializer.Serialize(validated, ReadableJson);
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text002"));
        // Все ошибки данных выявляются до открытия выходного файла.
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public static ExecutionScenario Validate(ExecutionScenario scenario, ExecutionSession session)
    {
        if (scenario.Format != "DSNCompiler.Scenario" || scenario.Version != 2)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text003"));
        if (scenario.Title is null || string.IsNullOrWhiteSpace(scenario.Title) || scenario.Title.Length > 200)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text004"));
        if (scenario.Identity is null || scenario.Identity != session.Identity)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text005"));
        PreparedProgramContext.Validate(scenario.PreparedProfile, session.Identity);
        if (session.Identity.DSNSha256.Length != 64) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text006"));
        if (scenario.Steps is < 1 or > ExecutionSession.MaxTicks)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text007"));
        if (scenario.InitialOverrides is null || scenario.Events is null || scenario.Observe is null ||
            scenario.Events.Length > MaxAssignments || scenario.InitialOverrides.Count > session.Variables.Count)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text008"));
        var names = session.Variables.ToDictionary(n => n, StringComparer.OrdinalIgnoreCase);
        string Word(string name) => name is not null && names.TryGetValue(name, out var canonical) ? canonical
            : throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionScenario.Text009"), name));
        var initial = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in scenario.InitialOverrides)
            if (!initial.TryAdd(Word(name), value)) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionScenario.Text010"), name));
        session.ValidateInitialOverrides(initial);
        var events = new List<InputEvent>(); var ticks = new HashSet<int>(); var assignments = 0;
        foreach (var entry in scenario.Events)
        {
            if (entry is null || entry.Tick < 1 || entry.Tick > scenario.Steps || !ticks.Add(entry.Tick) || entry.Values is null || entry.Values.Count == 0)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text011"));
            assignments += entry.Values.Count;
            if (assignments > MaxAssignments) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text012"));
            var values = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase); var addresses = new HashSet<int>();
            foreach (var (name, value) in entry.Values)
            {
                var word = Word(name);
                if (!session.Profile.InputWords.Contains(word, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionScenario.Text013"), name));
                if (!values.TryAdd(word, value) || !addresses.Add(session.Bound.Program.Addresses[word]))
                    throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionScenario.Text014"), name));
            }
            events.Add(new() { Tick = entry.Tick, Values = values.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary() });
        }
        if (scenario.Observe.Length > session.Variables.Count) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text015"));
        var observe = scenario.Observe.Select(Word).ToArray();
        if (observe.Distinct(StringComparer.OrdinalIgnoreCase).Count() != observe.Length) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text016"));
        if (scenario.Expectations?.Length > MaxExpectations) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text017"));
        var expectations = new List<ScenarioExpectation>(); var checks = new HashSet<(int Tick, string Word)>();
        foreach (var entry in scenario.Expectations ?? [])
        {
            if (entry is null || entry.Tick < 0 || entry.Tick > scenario.Steps || entry.Mask == 0 ||
                entry.Mask is { } mask && (entry.Value & ~mask) != 0)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text018"));
            var word = Word(entry.Word);
            if (!checks.Add((entry.Tick, word))) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionScenario.Text019", word, entry.Tick));
            expectations.Add(entry with { Word = word });
        }
        return new() { PreparedProfile = scenario.PreparedProfile is { } embedded ? ProgramDataContext.Clone(embedded) : PreparedProgramContext.Capture(session), Title = scenario.Title.Trim(), Identity = session.Identity, Steps = scenario.Steps,
            InitialOverrides = initial.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(), Events = events.OrderBy(e => e.Tick).ToArray(),
            Observe = observe.Order(StringComparer.Ordinal).ToArray(), Expectations = expectations.Count == 0 ? null : expectations.OrderBy(e => e.Tick).ThenBy(e => e.Word, StringComparer.Ordinal).ToArray() };
    }
}
