// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DSN.Execution;

internal sealed record AssemblyLine(string Text, SourceLocation Source);
internal sealed record AssemblyMacro(string Name, string[] Parameters, AssemblyLine[] Lines, SourceLocation Source);

public sealed class AssemblyLibrary : IFunctionLibrary
{
    public FunctionSemantics? Metadata { get; private set; }
    internal Dictionary<string, AssemblyMacro> Macros { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Architecture { get; }
    public string Sha256 { get; private set; } = "";
    public IReadOnlyDictionary<string, FunctionSignature> Signatures => Macros.ToDictionary(p => p.Key,
        p => new FunctionSignature(p.Key, p.Value.Parameters.Length, p.Value.Source), StringComparer.OrdinalIgnoreCase);
    private AssemblyLibrary(string architecture)
    {
        if (architecture is not ("RV32I" or "MSP430")) throw new ArgumentException(DSNText.Russian("Execution.AssemblyLibrary.Text001"));
        Architecture = architecture;
    }
    public static AssemblyLibrary ReadMany(IReadOnlyList<string> paths, string architecture)
    {
        if (paths.Count is < 1 or > 8) throw new ArgumentException(DSNText.Russian("Execution.AdspLibrary.Text002"));
        var result = new AssemblyLibrary(architecture);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > 1_048_576) throw new InvalidDataException(DSNText.Russian("Execution.AdspLibrary.Text001"));
            var library = Parse(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'), architecture, path);
            var digest = SHA256.HashData(bytes); hash.AppendData(digest);
            foreach (var (name, macro) in library.Macros)
                if (!result.Macros.TryAdd(name, macro)) throw new AdspException(macro.Source, DSNText.Concat(DSNText.Russian("Execution.AssemblyLibrary.Text002"), name));
            result.Metadata = FunctionSemantics.Merge(result.Metadata, library.Metadata);
            if (paths.Count == 1) result.Sha256 = Convert.ToHexString(digest);
        }
        if (paths.Count != 1) result.Sha256 = Convert.ToHexString(hash.GetHashAndReset());
        return result;
    }
    public static AssemblyLibrary Parse(string text, string architecture, string sourceName = "проверка.INC")
    {
        if (text.Length > 1_048_576) throw new InvalidDataException(DSNText.Russian("Execution.AdspLibrary.Text004"));
        var result = new AssemblyLibrary(architecture) { Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))) };
        var metadataText = text;
        // Многострочный комментарий удаляется с сохранением номеров строк.
        text = Regex.Replace(text, @"/\*[\s\S]*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')));
        if (text.Contains("/*") || text.Contains("*/")) throw new InvalidDataException(DSNText.Russian("Execution.AssemblyLibrary.Text003"));
        string? name = null; string[] parameters = []; SourceLocation? start = null;
        var body = new List<AssemblyLine>(); var number = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            number++;
            var line = raw.Split("//", 2)[0];
            var comment = line.IndexOf(architecture == "RV32I" ? '#' : ';');
            if (comment >= 0) line = line[..comment];
            line = line.Trim(); if (line.Length == 0) continue;
            var location = new SourceLocation(sourceName, number);
            if (line.StartsWith(".macro ", StringComparison.OrdinalIgnoreCase))
            {
                if (name is not null) throw new AdspException(location, DSNText.Russian("Execution.AssemblyLibrary.Text004"));
                var declaration = line[7..].Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
                name = declaration[0]; start = location;
                parameters = declaration.Length == 1 ? [] : declaration[1].Split(',').Select(p => p.Trim()).ToArray();
                if (!AdspLibrary.Identifier(name) || parameters.Length > 10 || parameters.Any(p => !AdspLibrary.Identifier(p)) ||
                    parameters.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.Length)
                    throw new AdspException(location, DSNText.Russian("Execution.AssemblyLibrary.Text005"));
                body.Clear();
            }
            else if (line.Equals(".endm", StringComparison.OrdinalIgnoreCase))
            {
                if (name is null || !result.Macros.TryAdd(name, new(name, parameters, body.ToArray(), start!)))
                    throw new AdspException(location, DSNText.Russian("Execution.AssemblyLibrary.Text006"));
                name = null;
            }
            else
            {
                if (name is null) throw new AdspException(location, DSNText.Russian("Execution.AssemblyLibrary.Text007"));
                body.Add(new(line, location));
            }
            if (number > 20_000 || body.Count > 4096 || result.Macros.Count > 256) throw new AdspException(location, DSNText.Russian("Execution.AdspLibrary.Text016"));
        }
        if (name is not null || result.Macros.Count == 0) throw new InvalidDataException(DSNText.Russian("Execution.AssemblyLibrary.Text008"));
        result.Metadata = FunctionSemantics.ParseAnnotations(metadataText, sourceName, result.Signatures);
        return result;
    }
}
