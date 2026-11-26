// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;

namespace DSN.Execution;

public sealed record ContextPreparation(VariableAnalysis Analysis, ExecutionProfile? Profile, string[] Diagnostics)
{
    public bool Ready => Profile is not null && Diagnostics.Length == 0;
}

public static class UserProgramPreparation
{
    public static ExecutionProfile StandardScope() => new()
    {
        Id = "DSN-standard-scope-v2", Bindings = "INC",
        Libraries = [ExecutionImplementation.StandardLibrary],
        OrderPolicy = ExecutionSchedule.ReferenceOrdinal
    };

    public static ContextPreparation Prepare(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog bindings,
        FunctionSemantics semantics, ExecutionProfile scope, ExecutionProfile? explicitContext = null)
    {
        RandomTestData.Validate(explicitContext?.RandomTestData);
        var analysis = VariableAnalyzer.Analyze(document, library, bindings, semantics, explicitContext);
        if (!analysis.CanPrepareContext) return new(analysis, null, analysis.Issues.Where(i => i.Severity == "ERROR").Select(i => DSNText.Concat(DSNText.Concat(i.Location, ": "), i.Message)).ToArray());
        var profile = new ExecutionProfile
        {
            Id = DSNText.Concat("DSN-user-", document.SourceSha256[..Math.Min(16, document.SourceSha256.Length)]), PreparedDSNSha256 = document.SourceSha256,
            Libraries = scope.Libraries.ToArray(), Bindings = scope.Bindings, OrderPolicy = ExecutionSchedule.ReferenceOrdinal,
            CompatibilityConfirmed = false,
            Description = DSNText.Russian("Execution.UserProgramPreparation.Text001"),
            RandomTestData = explicitContext?.RandomTestData is { } random ? ProgramDataContext.Clone(explicitContext).RandomTestData : null,
            Constants = new(explicitContext?.Constants ?? [], StringComparer.OrdinalIgnoreCase),
            PortConstants = explicitContext?.PortConstants is { Count: > 0 } supplied ? new(supplied, StringComparer.Ordinal) : null,
            InitialWords = new(explicitContext?.InitialWords ?? [], StringComparer.OrdinalIgnoreCase),
            EnvironmentWords = new(explicitContext?.EnvironmentWords ?? [], StringComparer.OrdinalIgnoreCase),
            BeforeTick = explicitContext?.BeforeTick.ToArray() ?? [],
            Assumptions = (explicitContext?.Assumptions ?? []).Concat(new[] {
                DSNText.Concat(DSNText.Russian("Execution.UserProgramPreparation.Text002"), semantics.Sha256),
                DSNText.Russian("Execution.UserProgramPreparation.Text003"),
                DSNText.Russian("Execution.UserProgramPreparation.Text004") })
                .Concat(explicitContext?.RandomTestData is { Length: > 0 } ? new[] { DSNText.Russian("Random.ProvenanceNote") } : [])
                .Distinct(StringComparer.Ordinal).ToArray(),
            VerifiedLibraries = semantics.Libraries.Select(l => l.Sha256).Order(StringComparer.Ordinal).ToArray(), Steps = 1
        };
        foreach (var variable in analysis.Variables.Where(v => v.Storage == "constant" && v.ConstantValue.HasValue))
            foreach (var name in variable.Names.Where(AdspLibrary.Identifier)) profile.Constants[name] = variable.ConstantValue!.Value;
        var used = analysis.Variables.SelectMany(v => v.Names).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inputs = new List<string>(); var aliases = new Dictionary<string, string>(explicitContext?.PortAliases ?? [], StringComparer.Ordinal); var index = 0;
        foreach (var variable in analysis.Variables.Where(v => v.Storage == "word16"))
        {
            var name = variable.Names.Where(n => AdspLibrary.Identifier(n) && !profile.Constants.ContainsKey(n)).Order(StringComparer.Ordinal).FirstOrDefault();
            if (name is null)
            {
                do { name = DSNText.Concat((variable.ExternalInputCandidate ? "DSN_INPUT_" : "DSN_VALUE_"), (++index).ToString("D4", System.Globalization.CultureInfo.InvariantCulture)); } while (!used.Add(name));
                aliases.Add(variable.Evidence[0].Endpoint, name);
            }
            if (variable.ExternalInputCandidate) inputs.Add(name);
        }
        profile.PortAliases = aliases.Count == 0 ? null : aliases;
        profile.InputWords = inputs.Concat(explicitContext?.InputWords ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        try
        {
            var program = DSNProgramBinder.Bind(document, library, bindings, profile).Program;
            profile.Observe = program.Addresses.Keys.Where(n => !n.StartsWith("__WORD_", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
            return new(analysis, profile, []);
        }
        catch (Exception error) when (error is AdspException or InvalidDataException or ArgumentException)
        { return new(analysis, null, [error.Message]); }
    }
}
