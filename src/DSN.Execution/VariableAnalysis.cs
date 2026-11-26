// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;

namespace DSN.Execution;

public sealed record DataIssue(string Severity, string Code, string Location, string Message);
public sealed record PortEvidence(string Endpoint, string Function, string Access, string Role, string WritesDomain,
    string[] BooleanDependencies, bool Initialized, string? InitializationGroup, string SourceFile, int SourceLine);
public sealed record VariableData(string Group, string[] Names, string Storage, int Words, string ExpectedType,
    bool BooleanInvariant, string BooleanBasis, bool ExternalInputCandidate, int? ConstantValue,
    string ConstantOrigin, string[] Roles, PortEvidence[] Evidence)
{
    public string DisplayName => Names.Length == 0 ? DSNText.Concat(DSNText.Russian("Execution.VariableAnalysis.Text001"), Group) : DSNText.Join(" / ", Names);
    public string DisplayRoles => DSNText.Join(", ", Roles.Select(r => r switch {
        "logical" => DSNText.Russian("Execution.VariableAnalysis.Text002"), "data" => DSNText.Russian("Execution.VariableAnalysis.Text003"), "bit-word" => DSNText.Russian("Execution.VariableAnalysis.Text004"), "mask" => DSNText.Russian("Execution.VariableAnalysis.Text005"),
        "tick-setting" => DSNText.Russian("Execution.VariableAnalysis.Text006"), "initial-setting" => DSNText.Russian("Execution.VariableAnalysis.Text007"), "initializer" => DSNText.Russian("Execution.VariableAnalysis.Text008"),
        "counter" => DSNText.Russian("Execution.VariableAnalysis.Text009"), "selection" => DSNText.Russian("Execution.VariableAnalysis.Text010"), "selection-default" => DSNText.Russian("Execution.VariableAnalysis.Text011"), "literal" => DSNText.Russian("Execution.VariableAnalysis.Text012"), _ => r }));
    public string DisplayType => ExpectedType switch { "boolean" => DSNText.Russian("Execution.VariableAnalysis.Text013"), "mask16" => DSNText.Russian("Execution.VariableAnalysis.Text005"), "numeric16" => DSNText.Russian("Execution.VariableAnalysis.Text014"), "literal16" => DSNText.Russian("Execution.VariableAnalysis.Text015"), "word16" => DSNText.Russian("Execution.VariableAnalysis.Text016"), "conflict" => DSNText.Russian("Execution.VariableAnalysis.Text017"), _ => DSNText.Russian("Execution.VariableAnalysis.Text018") };
    public string DisplayBoolean => BooleanInvariant ? DSNText.Russian("Execution.VariableAnalysis.Text019") : DSNText.Russian("Execution.VariableAnalysis.Text020");
    public string DisplayEvidence => DSNText.Concat(DSNText.Concat(BooleanBasis, "\n"), DSNText.Join("\n", Evidence.Select(e =>
        DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Concat(e.Endpoint, " · "), e.Function), " · "), e.SourceFile), ":"), e.SourceLine))));
}
public sealed record VariableAnalysis(string Format, int Version, string DSNSha256, string LibrarySha256, string SemanticsSha256,
    bool VerifiedLibrary, bool ExecutionChecked, string GuaranteeScope,
    VariableData[] Variables, DataIssue[] Issues)
{
    public bool CanPrepareContext => VerifiedLibrary && Issues.All(i => i.Severity != "ERROR");
}

public static class VariableAnalyzer
{
    private sealed class GroupState(string group, string[] names)
    {
        public string Group { get; } = group;
        public string[] Names { get; set; } = names;
        public List<PortEvidence> Uses { get; } = [];
        public List<string> Kinds { get; } = [];
        public int Words { get; set; } = 1;
        public bool Unknown { get; set; }
        public int? Constant { get; set; }
        public List<int> SuppliedConstants { get; } = [];
        public string Origin { get; set; } = "";
        public bool InitialKnown { get; set; } = true;
        public int Initial { get; set; }
        public string Expected { get; set; } = "unknown";
        public bool Invariant { get; set; }
        public bool External => !Unknown && Kinds.Contains("word") && Uses.Any(u => Reads(u.Access)) &&
            !Uses.Any(u => Writes(u.Access) || u.Initialized || u.Access == "initialize");
    }
    private static bool Reads(string access) => access is "read" or "readwrite";
    private static bool Writes(string access) => access is "write" or "readwrite";

    public static VariableAnalysis Analyze(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog bindings,
        FunctionSemantics semantics, ExecutionProfile? context = null)
    {
        if (document.Pages.Count != 1) throw new InvalidDataException(DSNText.Russian("Execution.VariableAnalysis.Text022"));
        var page = document.Pages[0]; var graph = ConnectionGraph.Build(document, page); var issues = new List<DataIssue>();
        var groups = graph.Ports.Values.GroupBy(p => p.Group).ToDictionary(g => g.Key,
            g => new GroupState(g.Key, g.SelectMany(p => p.Names).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()), StringComparer.Ordinal);
        var verified = semantics.Libraries.Any(l => l.Sha256 == library.Sha256);
        void Issue(string code, string location, string message, string severity = "ERROR") => issues.Add(new(severity, code, location, message));
        foreach (var (endpoint, alias) in context?.PortAliases ?? [])
            if (graph.Ports.TryGetValue(endpoint, out var connection) && AdspLibrary.Identifier(alias))
                groups[connection.Group].Names = groups[connection.Group].Names.Append(alias).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            else Issue("PORT_ALIAS_INVALID", endpoint, DSNText.Russian("Execution.VariableAnalysis.Text023"));
        if (!verified) Issue("LIBRARY_NOT_VERIFIED", "INC", DSNText.Russian("Execution.VariableAnalysis.Text024"));
        foreach (var instance in page.Instances)
        {
            var symbol = document.Symbol(instance);
            var known = verified && library.Signatures.TryGetValue(instance.Value, out _) &&
                bindings.Functions.ContainsKey(instance.Value) && semantics.Functions.ContainsKey(instance.Value);
            if (!known)
            {
                Issue("FUNCTION_CONTRACT_MISSING", instance.Reference, DSNText.Concat(DSNText.Russian("Execution.VariableAnalysis.Text025"), instance.Value));
                foreach (var port in symbol.Ports) groups[graph.Ports[DSNText.Concat(DSNText.Concat(instance.Reference, "."), port.Name)].Group].Unknown = true;
                continue;
            }
            var function = semantics.Functions[instance.Value]; var parameters = bindings.Functions[instance.Value];
            if (library.Signatures[instance.Value].Parameters != parameters.Length || parameters.Length != function.Ports.Length ||
                !parameters.Select(p => p.Port).Order(StringComparer.Ordinal).SequenceEqual(symbol.Ports.Select(p => p.Name).Order(StringComparer.Ordinal)) ||
                parameters.Where((p, i) => p.Port != function.Ports[i].Port || p.Kind != function.Ports[i].Kind || p.Words != function.Ports[i].Words).Any())
            {
                Issue("PORT_CONTRACT_MISMATCH", instance.Reference, DSNText.Russian("Execution.VariableAnalysis.Text026"));
                foreach (var port in symbol.Ports) groups[graph.Ports[DSNText.Concat(DSNText.Concat(instance.Reference, "."), port.Name)].Group].Unknown = true;
                continue;
            }
            var source = library.Signatures[instance.Value].Source;
            foreach (var port in function.Ports)
            {
                var endpoint = DSNText.Concat(DSNText.Concat(instance.Reference, "."), port.Port); var group = groups[graph.Ports[endpoint].Group];
                group.Kinds.Add(port.Kind); group.Words = Math.Max(group.Words, port.Words);
                group.Uses.Add(new(endpoint, instance.Value, port.Access, port.Role, port.WritesDomain,
                    port.BooleanInputs.Select(p => graph.Ports[DSNText.Concat(DSNText.Concat(instance.Reference, "."), p)].Group).ToArray(), port.Initialized,
                    port.InitializationPort is { } initial ? graph.Ports[DSNText.Concat(DSNText.Concat(instance.Reference, "."), initial)].Group : null,
                    Path.GetFileName(source.File), source.Line));
            }
            foreach (var environment in function.Environment)
                if (context?.EnvironmentWords.Keys.Contains(environment, StringComparer.OrdinalIgnoreCase) != true)
                    Issue("ENVIRONMENT_MISSING", instance.Reference, DSNText.Concat(DSNText.Russian("Execution.VariableAnalysis.Text027"), environment));
        }
        foreach (var (endpoint, value) in context?.PortConstants ?? [])
            if (graph.Ports.TryGetValue(endpoint, out var connection) && groups[connection.Group].Uses.Any(u => u.Endpoint == endpoint && u.Access == "constant"))
                groups[connection.Group].SuppliedConstants.Add(value);
            else Issue("CONSTANT_PORT_INVALID", endpoint, DSNText.Russian("Execution.VariableAnalysis.Text028"));
        foreach (var group in groups.Values)
        {
            if (group.Kinds.Contains("word") && group.Kinds.Contains("constant"))
                Issue("STORAGE_CONFLICT", group.Group, DSNText.Russian("Execution.VariableAnalysis.Text029"));
            if (!group.Kinds.Contains("constant")) continue;
            var values = new HashSet<int>(); var origins = new HashSet<string>(StringComparer.Ordinal);
            if (group.SuppliedConstants.Count > 0) { values.UnionWith(group.SuppliedConstants); origins.Add("profile-port"); }
            else foreach (var name in group.Names)
            {
                var declared = context?.Constants.Where(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
                if (declared.Length > 0) { foreach (var pair in declared) values.Add(pair.Value); origins.Add("profile"); }
                else if (DSNProgramBinder.TryConstant(name, out var value)) { values.Add(value); origins.Add("DSN-literal"); }
            }
            if (values.Count != 1) Issue(values.Count == 0 ? "CONSTANT_MISSING" : "CONSTANT_AMBIGUOUS", group.Group,
                values.Count == 0 ? DSNText.Russian("Execution.VariableAnalysis.Text030") : DSNText.Russian("Execution.VariableAnalysis.Text031"));
            else if (values.Single() is < -32768 or > 65535) Issue("CONSTANT_RANGE", group.Group, DSNText.Russian("Execution.VariableAnalysis.Text032"));
            else { group.Constant = values.Single(); group.Origin = DSNText.Join("+", origins.Order(StringComparer.Ordinal)); }
        }
        foreach (var group in groups.Values)
        {
            var initial = context?.InitialWords.Where(p => group.Names.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).Select(p => (int)p.Value).Distinct().ToArray() ?? [];
            if (initial.Length > 1) { group.InitialKnown = false; Issue("INITIAL_ALIAS_CONFLICT", group.Group, DSNText.Russian("Execution.VariableAnalysis.Text033")); }
            else if (initial.Length == 1) group.Initial = initial[0];
            var initialized = group.Uses.Where(u => u.Initialized || u.Access == "initialize").ToArray();
            if (initialized.Length > 0)
            {
                var values = initialized.Select(u => u.InitializationGroup is { } id ? groups[id].Constant : null).ToArray();
                group.InitialKnown = values.All(v => v.HasValue) && values.Distinct().Count() == 1;
                if (group.InitialKnown) group.Initial = values[0]!.Value;
            }
            var writers = group.Uses.Where(u => Writes(u.Access)).ToArray();
            group.Expected = group.Unknown ? "unknown" : group.Kinds.Distinct().Count() > 1 ? "conflict" : group.Kinds.Contains("constant")
                ? group.Uses.Any(u => u.Role == "mask") ? "mask16" : "literal16"
                : writers.Length > 0 && writers.All(u => u.WritesDomain == "boolean") ? "boolean"
                : writers.Any(u => u.WritesDomain == "word16") ? "word16"
                : group.Uses.Any(u => u.Role is "counter" or "tick-setting") ? "numeric16"
                : group.Uses.Where(u => Reads(u.Access)).All(u => u.Role == "logical") && group.Uses.Any(u => Reads(u.Access)) ? "boolean" : "word16";
        }
        for (var pass = 0; pass <= groups.Count; pass++)
        {
            var changed = false;
            foreach (var group in groups.Values.Where(g => !g.Unknown && g.Kinds.Distinct().Count() == 1 && g.Kinds.Contains("word")))
            {
                var writers = group.Uses.Where(u => Writes(u.Access)).ToArray();
                if (group.Expected == "boolean" || writers.Length == 0 || !writers.All(u => u.WritesDomain == "boolean" ||
                    u.WritesDomain == "conditional-boolean" && u.BooleanDependencies.All(d => groups[d].Expected == "boolean"))) continue;
                group.Expected = "boolean"; changed = true;
            }
            if (!changed) break;
        }
        foreach (var group in groups.Values)
            group.Invariant = group.Expected == "boolean" && group.Words == 1 && !group.External && !group.Unknown && group.InitialKnown && group.Initial is 0 or 1 &&
                !(context?.InputWords.Any(n => group.Names.Contains(n, StringComparer.OrdinalIgnoreCase)) ?? false) &&
                group.Uses.Where(u => Writes(u.Access)).All(u => u.WritesDomain is "boolean" or "conditional-boolean");
        for (var pass = 0; pass <= groups.Count; pass++)
        {
            var changed = false;
            foreach (var group in groups.Values.Where(g => g.Invariant))
                if (group.Uses.Where(u => Writes(u.Access) && u.WritesDomain == "conditional-boolean").Any(u => u.BooleanDependencies.Any(d => !groups[d].Invariant)))
                { group.Invariant = false; changed = true; }
            if (!changed) break;
        }
        if (context?.BeforeTick.Length > 0)
            foreach (var group in groups.Values) group.Invariant = false;
        var variables = groups.Values.OrderBy(g => g.Group, StringComparer.Ordinal).Select(g => new VariableData(g.Group, g.Names,
            g.Unknown ? "unknown" : g.Kinds.Distinct().Count() > 1 ? "conflict" : g.Kinds.Contains("constant") ? "constant" : "word16", g.Unknown ? 0 : g.Words, g.Expected, g.Invariant,
            g.Invariant ? DSNText.Russian("Execution.VariableAnalysis.Text034") : g.Expected == "boolean" ? DSNText.Russian("Execution.VariableAnalysis.Text035") : DSNText.Russian("Execution.VariableAnalysis.Text036"),
            g.External, g.Constant, g.Origin, g.Uses.Select(u => u.Role).Distinct().Order(StringComparer.Ordinal).ToArray(),
            g.Uses.OrderBy(u => u.Endpoint, StringComparer.Ordinal).ToArray())).ToArray();
        return new("DSNCompiler.VariableAnalysis", 1, document.SourceSha256, library.Sha256, semantics.Sha256, verified,
            false,
            DSNText.Russian("Execution.VariableAnalysis.Text037"), variables,
            issues.Distinct().OrderBy(i => i.Location, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
    }
}
