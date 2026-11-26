// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSN.Core;

namespace DSN.Execution;

public sealed record ParameterBinding([property: JsonRequired] string Port, [property: JsonRequired] string Kind, int Words = 1);
public sealed class BindingCatalog
{
    public static BindingCatalog FromLibrary(IFunctionLibrary library) => FunctionSemantics.FromLibrary(library).ToBindings();
    public string Version { get; set; } = "";
    public Dictionary<string, ParameterBinding[]> Functions { get; set; } = [];
    public string[] EnvironmentWords { get; set; } = [];
}
public sealed class InputEvent
{
    public int Tick { get; set; }
    public Dictionary<string, ushort> Values { get; set; } = [];
}
public sealed class ExecutionProfile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public RandomTestBatch[]? RandomTestData { get; set; }
    public string Id { get; set; } = "";
    public bool CompatibilityConfirmed { get; set; }
    public string[] Libraries { get; set; } = [ExecutionImplementation.StandardLibrary];
    public string Bindings { get; set; } = "INC";
    public string Description { get; set; } = "";
    public string[] Assumptions { get; set; } = [];
    public Dictionary<string, int> Constants { get; set; } = [];
    public Dictionary<string, ushort> InitialWords { get; set; } = [];
    public Dictionary<string, ushort> EnvironmentWords { get; set; } = [];
    public string[] InputWords { get; set; } = [];
    public string[] Order { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OrderPolicy { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, string>? PortAliases { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, int>? PortConstants { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? VerifiedLibraries { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PreparedDSNSha256 { get; set; }
    public int Steps { get; set; }
    public InputEvent[] Events { get; set; } = [];
    public MacroInvocation[] BeforeTick { get; set; } = [];
    public string[] Observe { get; set; } = [];
}

public sealed record ValueBinding(string? Word, int? Constant);
public sealed record BoundDSNProgram(IExecutionProgram Program, IReadOnlyList<MacroInvocation> Invocations,
    IReadOnlyDictionary<string, ValueBinding> Ports, IReadOnlyDictionary<uint, ValueBinding> Links)
{
    public ExecutionSchedule Schedule { get; init; } = null!;
}

public static class DSNProgramBinder
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static T ReadJson<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text001"), path));

    public static BoundDSNProgram Bind(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog catalog, ExecutionProfile profile)
    {
        var schedule = ExecutionSchedule.Create(document, profile);
        if (profile.PreparedDSNSha256 is { } preparedHash && preparedHash != document.SourceSha256)
            throw new InvalidDataException(DSNText.Russian("Execution.DSNProgramBinder.Text002"));
        if (profile.VerifiedLibraries is { } expectedLibraries && !expectedLibraries.Contains(library.Sha256, StringComparer.Ordinal))
            throw new InvalidDataException(DSNText.Russian("Execution.DSNProgramBinder.Text003"));
        var page = document.Pages[0]; var graph = ConnectionGraph.Build(document, page);
        var instances = page.Instances.ToDictionary(i => i.Reference, StringComparer.OrdinalIgnoreCase);
        var portConstants = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var (endpoint, value) in profile.PortConstants ?? [])
        {
            if (!graph.Ports.TryGetValue(endpoint, out var connection) || value is < -32768 or > 65535 ||
                !instances.Values.Any(i => catalog.Functions.TryGetValue(i.Value, out var parameters) && parameters.Any(p => p.Kind == "constant" && DSNText.Concat(DSNText.Concat(i.Reference, "."), p.Port) == endpoint)))
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text004"), endpoint));
            if (!portConstants.TryGetValue(connection.Group, out var values)) portConstants[connection.Group] = values = [];
            values.Add(value);
        }
        var constants = new Dictionary<string, int>(profile.Constants, StringComparer.OrdinalIgnoreCase);
        var words = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var groupWords = new Dictionary<string, string>(StringComparer.Ordinal);
        var groupSizes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var instance in page.Instances)
            if (catalog.Functions.TryGetValue(instance.Value, out var specification))
                foreach (var parameter in specification)
                {
                    if (parameter.Words is < 1 or > 2 || parameter.Kind != "word" && parameter.Words != 1) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text005"), instance.Reference));
                    if (parameter.Kind == "word" && graph.Ports.TryGetValue(DSNText.Concat(DSNText.Concat(instance.Reference, "."), parameter.Port), out var port))
                        groupSizes[port.Group] = Math.Max(groupSizes.GetValueOrDefault(port.Group, 1), parameter.Words);
                }
        var index = 0;
        foreach (var group in graph.Ports.Values.GroupBy(p => p.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var name = DSNText.Concat("__WORD_", (++index).ToString("D4", CultureInfo.InvariantCulture));
            words.Add(name, 0); groupWords.Add(group.Key, name);
            if (groupSizes.GetValueOrDefault(group.Key, 1) == 2) words.Add(DSNText.Concat(name, "_HI"), 0);
            foreach (var alias in group.SelectMany(p => p.Names).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!AdspLibrary.Identifier(alias) || constants.ContainsKey(alias)) continue;
                if (aliases.TryGetValue(alias, out var old) && old != name) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text006"), alias));
                aliases[alias] = name;
                if (groupSizes.GetValueOrDefault(group.Key, 1) == 2)
                {
                    var highAlias = DSNText.Concat(alias, "_HI"); var highTarget = DSNText.Concat(name, "_HI");
                    if (aliases.TryGetValue(highAlias, out var highPrevious) && highPrevious != highTarget) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text007"), highAlias));
                    aliases[highAlias] = highTarget;
                }
            }
        }
        foreach (var (endpoint, alias) in profile.PortAliases ?? [])
        {
            if (!graph.Ports.TryGetValue(endpoint, out var port) || !AdspLibrary.Identifier(alias) || constants.ContainsKey(alias))
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text008"), endpoint));
            var target = groupWords[port.Group];
            if (aliases.TryGetValue(alias, out var existing) && existing != target) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text009"), alias));
            aliases[alias] = target;
            if (groupSizes.GetValueOrDefault(port.Group, 1) == 2)
            {
                if (aliases.TryGetValue(DSNText.Concat(alias, "_HI"), out var high) && high != DSNText.Concat(target, "_HI")) throw new InvalidDataException(DSNText.Russian("Execution.DSNProgramBinder.Text010"));
                aliases[DSNText.Concat(alias, "_HI")] = DSNText.Concat(target, "_HI");
            }
        }
        foreach (var (name, value) in profile.EnvironmentWords)
        {
            if (!catalog.EnvironmentWords.Contains(name, StringComparer.OrdinalIgnoreCase) || aliases.ContainsKey(name) || !words.TryAdd(name, value))
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text011"), name));
        }
        var seeded = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in profile.InitialWords)
        {
            if (!aliases.TryGetValue(name, out var target)) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text012"), name));
            if (seeded.TryGetValue(target, out var previous) && previous != value) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text013"), name));
            seeded[target] = value;
            words[target] = value;
        }
        var calls = new List<MacroInvocation>();
        var portBindings = new Dictionary<string, ValueBinding>(StringComparer.Ordinal);
        var groupBindings = new Dictionary<string, ValueBinding>(StringComparer.Ordinal);
        foreach (var id in schedule.Instances)
        {
            var instance = instances[id];
            if (!catalog.Functions.TryGetValue(instance.Value, out var parameters)) throw new InvalidDataException(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text014"), instance.Reference), " / "), instance.Value));
            var symbol = document.Symbol(instance);
            if (parameters.Length != symbol.Ports.Count || parameters.Select(p => p.Port).Distinct(StringComparer.Ordinal).Count() != symbol.Ports.Count ||
                symbol.Ports.Any(port => !parameters.Any(p => p.Port == port.Name))) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text015"), instance.Reference));
            var arguments = new List<string>();
            foreach (var parameter in parameters)
            {
                if (!graph.Ports.TryGetValue(DSNText.Concat(DSNText.Concat(instance.Reference, "."), parameter.Port), out var connection)) throw new InvalidDataException(DSNText.Russian("Execution.DSNProgramBinder.Text016"));
                ValueBinding binding;
                if (parameter.Kind == "word")
                {
                    arguments.Add(groupWords[connection.Group]);
                    binding = new(groupWords[connection.Group], null);
                }
                else if (parameter.Kind == "constant")
                {
                    var values = new HashSet<int>();
                    if (portConstants.TryGetValue(connection.Group, out var supplied)) values.UnionWith(supplied);
                    else foreach (var name in connection.Names)
                    {
                        if (constants.TryGetValue(name, out var value)) values.Add(value);
                        else if (TryConstant(name, out value)) values.Add(value);
                    }
                    if (values.Count != 1) throw new InvalidDataException(DSNText.Russian("Execution.DSNProgramBinder.Text017", instance.Reference, parameter.Port));
                    arguments.Add(values.Single().ToString(CultureInfo.InvariantCulture));
                    binding = new(null, values.Single());
                }
                else throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text018"), parameter.Kind));
                portBindings.Add(DSNText.Concat(DSNText.Concat(instance.Reference, "."), parameter.Port), binding);
                if (groupBindings.TryGetValue(connection.Group, out var existing) && existing != binding)
                    throw new InvalidDataException(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Execution.DSNProgramBinder.Text019"), instance.Reference), "."), parameter.Port));
                groupBindings[connection.Group] = binding;
            }
            calls.Add(new(instance.Reference, instance.Value, arguments));
        }
        var program = ExecutionCompiler.Compile(library, profile.BeforeTick.Concat(calls).ToArray(), words, constants, aliases);
        if (profile.InputWords.Distinct(StringComparer.OrdinalIgnoreCase).Count() != profile.InputWords.Length ||
            profile.InputWords.Concat(profile.Observe).Any(name => !program.Addresses.ContainsKey(name))) throw new InvalidDataException(DSNText.Russian("Execution.DSNProgramBinder.Text020"));
        return new(program, calls, portBindings, graph.Links.Where(w => groupBindings.ContainsKey(w.Value.Group))
            .ToDictionary(w => w.Key, w => groupBindings[w.Value.Group])) { Schedule = schedule };
    }
    public static bool TryConstant(string name, out int value)
    {
        if (int.TryParse(name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)) return true;
        if (name.StartsWith("B#", StringComparison.OrdinalIgnoreCase) && name.Length is > 2 and <= 18 && name[2..].All(c=>c is '0' or '1'))
        { value=Convert.ToInt32(name[2..],2); return true; }
        if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ushort.TryParse(name.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex))
        { value=hex; return true; }
        value=0; return false;
    }
}
