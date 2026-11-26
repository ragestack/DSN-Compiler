// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;

namespace DSN.Execution;

public sealed record FunctionReadiness(string Function, int Instances, bool SourceFound, bool PortsMatch,
    bool CompilesInIsolation, string Diagnostic, IReadOnlyList<string> References);
public sealed record ProgramReadiness(int Blocks, int Types, int SourceInstances, int CompilableInstances,
    string LibrarySha256, IReadOnlyList<FunctionReadiness> Functions)
{
    public static ProgramReadiness Analyze(LogicProgramDocument document, IFunctionLibrary library, BindingCatalog catalog)
    {
        var rows = new List<FunctionReadiness>();
        foreach (var group in document.Pages.SelectMany(p => p.Instances).GroupBy(i => i.Value).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var source = library.Signatures.TryGetValue(group.Key, out var macro);
            var portsMatch = catalog.Functions.TryGetValue(group.Key, out var parameters) && source && parameters.Length == macro!.Parameters &&
                group.All(i => document.Symbol(i).Ports.Select(p => p.Name).Order(StringComparer.Ordinal)
                    .SequenceEqual(parameters.Select(p => p.Port).Order(StringComparer.Ordinal)));
            var compiles = false; var diagnostic = !source ? DSNText.Russian("Execution.ProgramReadiness.Text001") : !portsMatch ? DSNText.Russian("Execution.ProgramReadiness.Text002") : "";
            if (portsMatch)
            {
                try
                {
                    var words = new Dictionary<string, ushort>(); var arguments = new List<string>();
                    foreach (var (parameter, index) in parameters!.Select((p, i) => (p, i)))
                    {
                        if (parameter.Kind == "constant") { arguments.Add("1"); continue; }
                        if (parameter.Kind != "word" || parameter.Words is < 1 or > 2) throw new InvalidDataException(DSNText.Russian("Execution.ProgramReadiness.Text003"));
                        var name = DSNText.Concat("P", index); arguments.Add(name); words.Add(name, 0);
                        if (parameter.Words == 2) words.Add(DSNText.Concat(name, "_HI"), 0);
                    }
                    foreach (var name in catalog.EnvironmentWords) words.Add(name, 0);
                    ExecutionCompiler.Compile(library, [new("CHECK", group.Key, arguments)], words);
                    compiles = true; diagnostic = DSNText.Russian("Execution.ProgramReadiness.Text005");
                }
                catch (Exception error) when (error is AdspException or InvalidDataException or ArgumentException) { diagnostic = error.Message; }
            }
            rows.Add(new(group.Key, group.Count(), source, portsMatch, compiles, diagnostic, group.Select(i => i.Reference).Order(StringComparer.Ordinal).ToArray()));
        }
        return new(rows.Sum(r => r.Instances), rows.Count, rows.Where(r => r.SourceFound).Sum(r => r.Instances),
            rows.Where(r => r.CompilesInIsolation).Sum(r => r.Instances), library.Sha256, rows);
    }
}
