// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Globalization;
using System.Text.Json.Serialization;

namespace DSN.Execution;

public sealed record SemanticLibrary(string Architecture, string Sha256);
public sealed record SemanticPort(string Port, string Kind, int Words, string Access, string Role,
    string WritesDomain, string[] BooleanInputs, bool Initialized, string? InitializationPort);
public sealed record FunctionSemantic(SemanticPort[] Ports, string[] Environment);

public sealed class FunctionSemantics
{
    // Закреплены после сравнения описаний и неизменности инструкций, не вычисляются из изменённого INC при запуске.
    private static readonly SemanticLibrary[] approved = [
        new("ADSP21", "9AB3E0B9A3586F38D98985A2495AD19FA7425EA77EC031E5FEBD58A90750645A"),
        new("RV32I", "FF846C0C5E943BF1E1BF4EED68DBE1156F1260DF2F16C959BFCCCD15F6F4C5AF"),
        new("MSP430", "291D0F1610F322239AE818D45567DF30CC492344FEBF171AC1FC6B91C86C3490")];
    public string Format { get; set; } = "DSNCompiler.FunctionSemantics";
    public int Version { get; set; } = 1;
    public string Author { get; set; } = "Автор и правообладатель: Леонов Денис <466611@mail.ru>";
    public string BindingVersion { get; set; } = "";
    public string[] EnvironmentWords { get; set; } = [];
    public SemanticLibrary[] Libraries { get; set; } = [];
    public Dictionary<string, FunctionSemantic> Functions { get; set; } = [];
    [JsonIgnore] public string Sha256 { get; private set; } = "";

    public BindingCatalog ToBindings() => new() { Version = BindingVersion, EnvironmentWords = EnvironmentWords.ToArray(),
        Functions = Functions.ToDictionary(p => p.Key, p => p.Value.Ports.Select(v => new ParameterBinding(v.Port, v.Kind, v.Words)).ToArray()) };

    public static FunctionSemantics FromLibrary(IFunctionLibrary library)
    {
        var source = library.Metadata ?? throw new InvalidDataException(DSNText.Russian("Contract.Missing"));
        var result = new FunctionSemantics { BindingVersion = source.BindingVersion, EnvironmentWords = source.EnvironmentWords.ToArray(),
            Libraries = approved.ToArray(), Functions = source.Functions.ToDictionary(p => p.Key,
                p => new FunctionSemantic(p.Value.Ports.Select(v => v with { BooleanInputs = v.BooleanInputs.ToArray() }).ToArray(), p.Value.Environment.ToArray())) };
        result.Sha256 = ScenarioFile.Fingerprint(result);
        return result;
    }

    public static FunctionSemantics? Merge(FunctionSemantics? left, FunctionSemantics? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        if (left.BindingVersion != right.BindingVersion || !left.EnvironmentWords.SequenceEqual(right.EnvironmentWords))
            throw new InvalidDataException(DSNText.Russian("Contract.Conflict"));
        foreach (var (name, definition) in right.Functions)
            if (!left.Functions.TryAdd(name, definition)) throw new InvalidDataException(DSNText.Russian("Contract.Conflict"));
        return left;
    }

    internal static FunctionSemantics? ParseAnnotations(string text, string file, IReadOnlyDictionary<string, FunctionSignature> signatures)
    {
        FunctionSemantics? result = null;
        string? function = null; string[] environment = []; var ports = new List<SemanticPort>();
        var lineNumber = 0;
        AdspException Error() => new(new(file, lineNumber), DSNText.Russian("Contract.Invalid"));
        string[] Csv(string value)
        {
            var names = value.Length == 0 ? [] : value.Split(',');
            if (names.Any(n => !AdspLibrary.Identifier(n)) || names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw Error();
            return names;
        }
        void Finish()
        {
            if (function is null) return;
            if (result is null || ports.Count == 0 || !signatures.TryGetValue(function, out var signature) ||
                signature.Parameters != ports.Count || !result.Functions.TryAdd(function, new(ports.ToArray(), environment))) throw Error();
            foreach (var port in ports)
                if (port.Kind is not ("word" or "constant") || port.Words is < 1 or > 2 ||
                    port.Access is not ("read" or "write" or "readwrite" or "constant" or "initialize") ||
                    port.WritesDomain is not ("none" or "boolean" or "word16" or "conditional-boolean") ||
                    port.Kind == "constant" && (port.Access != "constant" || port.Words != 1) ||
                    port.Kind == "word" && port.Access == "constant" ||
                    (port.Access is "write" or "readwrite") != (port.WritesDomain != "none") ||
                    (port.WritesDomain == "conditional-boolean") != (port.BooleanInputs.Length > 0) ||
                    string.IsNullOrWhiteSpace(port.Role) ||
                    port.BooleanInputs.Any(p => !ports.Any(s => s.Port == p && s.Kind == "word")) ||
                    port.InitializationPort is { } initial && !ports.Any(p => p.Port == initial && p.Kind == "constant")) throw Error();
            if (environment.Any(n => !result.EnvironmentWords.Contains(n, StringComparer.Ordinal))) throw Error();
            ports.Clear();
        }
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (!line.Contains("@DSN-", StringComparison.Ordinal)) continue;
            if (line.StartsWith("{ ", StringComparison.Ordinal) && line.EndsWith(" }", StringComparison.Ordinal)) line = line[2..^2];
            else if (line.StartsWith("/* ", StringComparison.Ordinal) && line.EndsWith(" */", StringComparison.Ordinal)) line = line[3..^3];
            else throw Error();
            var marker = line.Split(' ', 2);
            if (marker.Length != 2) throw Error();
            var fields = marker[1].Split('|');
            switch (marker[0])
            {
                case "@DSN-LIBRARY":
                    if (result is not null || fields.Length != 3 || fields[0] != "1" || string.IsNullOrWhiteSpace(fields[1]) || fields[1].Length > 64) throw Error();
                    result = new() { BindingVersion = fields[1], EnvironmentWords = Csv(fields[2]) }; break;
                case "@DSN-FUNCTION":
                    if (result is null || fields.Length != 2 || !AdspLibrary.Identifier(fields[0])) throw Error();
                    Finish(); function = fields[0]; environment = Csv(fields[1]); break;
                case "@DSN-PORT":
                    if (function is null || fields.Length != 9 || !AdspLibrary.Identifier(fields[0]) || ports.Any(p => p.Port == fields[0]) ||
                        !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var words) || !bool.TryParse(fields[7], out var initialized)) throw Error();
                    ports.Add(new(fields[0], fields[1], words, fields[3], fields[4], fields[5], Csv(fields[6]), initialized, fields[8].Length == 0 ? null : fields[8])); break;
                default: throw Error();
            }
        }
        Finish();
        if (result is not null && result.Functions.Count == 0) throw Error();
        return result;
    }
}
