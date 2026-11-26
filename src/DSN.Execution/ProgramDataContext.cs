// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Text.Json;
using DSN.Core;

namespace DSN.Execution;

public sealed record DataParameter(string Key, string Kind, string Name, string Purpose, int? ExistingValue, string Origin, string[] Ports)
{
    public string DisplayValue => ExistingValue is { } value ? DSNText.Concat(DSNText.Concat(value, " / 0x"), unchecked((ushort)value).ToString("X4")) : DSNText.Russian("Execution.ProgramDataContext.Text001");
}
public sealed record ProgramDataDraft(ExecutionProfile Context, Dictionary<string, string> Values)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RandomTestDraft? RandomSettings { get; init; }
}

public static class ProgramDataContext
{
    public static ExecutionProfile Clone(ExecutionProfile context) => ExecutionJson.Parse<ExecutionProfile>(JsonSerializer.Serialize(context, DSNProgramBinder.JsonOptions), 2 * 1024 * 1024);

    public static DataParameter[] Parameters(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog bindings,
        FunctionSemantics semantics, ExecutionProfile? context = null)
    {
        var analysis = VariableAnalyzer.Analyze(document, library, bindings, semantics, context);
        var constants = analysis.Variables.Where(v => v.Storage == "constant" && v.Evidence.Length > 0).Select(v =>
            new DataParameter(DSNText.Concat("constant:", v.Evidence[0].Endpoint), "constant", v.DisplayName, v.DisplayRoles, v.ConstantValue, v.ConstantOrigin,
                v.Evidence.Select(e => e.Endpoint).ToArray()));
        var environment = document.Pages.SelectMany(p => p.Instances).Where(i => semantics.Functions.ContainsKey(i.Value))
            .SelectMany(i => semantics.Functions[i.Value].Environment).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(name => new DataParameter(DSNText.Concat("environment:", name), "environment", name, DSNText.Russian("Execution.ProgramDataContext.Text002"),
                context?.EnvironmentWords.Where(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Select(p => (int?)p.Value).FirstOrDefault(), "profile", []));
        return constants.Concat(environment).OrderBy(p => p.Kind, StringComparer.Ordinal).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
    }

    public static ExecutionProfile Create(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog bindings,
        FunctionSemantics semantics, ExecutionProfile scope, ExecutionProfile? source, IReadOnlyDictionary<string, string> values)
    {
        if (source is not null)
        {
            Shape(source);
            if (source.PreparedDSNSha256 is { } hash && hash != document.SourceSha256 ||
                source.VerifiedLibraries is { } hashes && !hashes.Contains(library.Sha256, StringComparer.Ordinal))
                throw new InvalidDataException(DSNText.Russian("Execution.ProgramDataContext.Text003"));
        }
        var parameters = Parameters(document, library, bindings, semantics, source).ToDictionary(p => p.Key, StringComparer.Ordinal);
        var context = source is null ? new ExecutionProfile() : Clone(source);
        context.Id = DSNText.Concat("DSN-data-", document.SourceSha256[..16]); context.PreparedDSNSha256 = document.SourceSha256;
        context.Libraries = scope.Libraries.ToArray(); context.Bindings = scope.Bindings;
        context.CompatibilityConfirmed = false; context.OrderPolicy = ExecutionSchedule.ReferenceOrdinal; context.Order = [];
        context.Events = []; context.Observe = []; context.Steps = 1;
        context.Description = DSNText.Russian("Execution.ProgramDataContext.Text004");
        _ = semantics.Libraries.SingleOrDefault(l => l.Sha256 == library.Sha256)
            ?? throw new InvalidDataException(DSNText.Russian("Execution.ProgramDataContext.Text005"));
        context.VerifiedLibraries = semantics.Libraries.Select(l => l.Sha256).Order(StringComparer.Ordinal).ToArray();
        foreach (var (key, text) in values)
        {
            if (!parameters.TryGetValue(key, out var parameter)) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ProgramDataContext.Text006"), key));
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (text.Length > 128 || !DSNProgramBinder.TryConstant(text.Trim(), out var value) || value < (parameter.Kind == "constant" ? -32768 : 0) || value > 65535)
                throw new InvalidDataException(DSNText.Concat(DSNText.Concat(DSNText.Concat(parameter.Name, DSNText.Russian("Execution.ProgramDataContext.Text007")), (parameter.Kind == "constant" ? "−32768…65535" : "0…65535")), DSNText.Russian("Execution.ProgramDataContext.Text008")));
            if (parameter.Kind == "environment") context.EnvironmentWords[parameter.Name] = (ushort)value;
            else
            {
                context.PortConstants ??= new(StringComparer.Ordinal);
                foreach (var endpoint in parameter.Ports) context.PortConstants.Remove(endpoint);
                context.PortConstants[parameter.Ports[0]] = value;
            }
        }
        var analysis = VariableAnalyzer.Analyze(document, library, bindings, semantics, context);
        var errors = analysis.Issues.Where(i => i.Severity == "ERROR" && i.Code is not ("CONSTANT_MISSING" or "ENVIRONMENT_MISSING")).ToArray();
        if (errors.Length > 0) throw new InvalidDataException(DSNText.Join("\n", errors.Take(8).Select(e => DSNText.Concat(DSNText.Concat(e.Location, ": "), e.Message))));
        var prepared = UserProgramPreparation.Prepare(document, library, bindings, semantics, scope, context);
        if (analysis.CanPrepareContext && !prepared.Ready) throw new InvalidDataException(DSNText.Join("\n", prepared.Diagnostics));
        return context;
    }

    public static ExecutionProfile Read(string path, LogicProgramDocument document, IFunctionLibrary library, BindingCatalog bindings, FunctionSemantics semantics, ExecutionProfile scope)
    {
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException(DSNText.Russian("Execution.ProgramDataContext.Text009"));
        var context = ExecutionJson.Parse<ExecutionProfile>(File.ReadAllText(path).TrimStart('\uFEFF'), 2 * 1024 * 1024);
        Shape(context);
        if (context.PreparedDSNSha256 != document.SourceSha256 || context.VerifiedLibraries is not { Length: > 0 } ||
            !context.Libraries.SequenceEqual(scope.Libraries) || context.Bindings != scope.Bindings)
            throw new InvalidDataException(DSNText.Russian("Execution.ProgramDataContext.Text010"));
        return Create(document, library, bindings, semantics, scope, context, new Dictionary<string, string>());
    }

    public static void Save(string path, ExecutionProfile context)
    {
        Shape(context); var json = JsonSerializer.Serialize(context, DSNProgramBinder.JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) throw new InvalidDataException(DSNText.Russian("Execution.ProgramDataContext.Text011"));
        var destination = Path.GetFullPath(path); var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, json); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void Shape(ExecutionProfile context)
    {
        if (context is null || context.CompatibilityConfirmed || context.Constants is null || context.EnvironmentWords is null || context.InitialWords is null ||
            context.Libraries is null || context.Libraries.Length is < 1 or > 2 || context.Assumptions is null || context.BeforeTick is null ||
            context.InputWords is null || context.Events is null || context.Order is null || context.Observe is null ||
            context.Constants.Values.Any(v => v is < -32768 or > 65535) || context.PortConstants?.Values.Any(v => v is < -32768 or > 65535) == true)
            throw new InvalidDataException(DSNText.Russian("Execution.ProgramDataContext.Text012"));
        RandomTestData.Validate(context.RandomTestData);
    }
}
