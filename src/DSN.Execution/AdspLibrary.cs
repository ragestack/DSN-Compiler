// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DSN.Execution;

public sealed record SourceLocation(string File, int Line, string Instance = "", string Macro = "")
{
    public override string ToString() => DSNText.Concat(DSNText.Compose($"{File}:{Line}"), (Instance.Length == 0 ? "" : DSNText.Compose($" [{Instance}, {Macro}]")));
}

public sealed class AdspException(SourceLocation source, string message) : Exception(DSNText.Compose($"{source}: {message}"))
{
    public SourceLocation Location { get; } = source;
}

internal sealed record Token(string Text, SourceLocation Source);
internal sealed record Statement(IReadOnlyList<Token> Tokens, SourceLocation Source);
internal sealed record MacroDefinition(string Name, string[] Parameters, IReadOnlyList<Statement> Body, SourceLocation Source);

public sealed class AdspLibrary : IFunctionLibrary
{
    public FunctionSemantics? Metadata { get; private set; }
    private readonly Dictionary<string, MacroDefinition> macros = new(StringComparer.OrdinalIgnoreCase);
    public string Sha256 { get; private set; }
    public IReadOnlyCollection<string> MacroNames => macros.Keys.ToArray();
    public IReadOnlyDictionary<string, FunctionSignature> Signatures => macros.ToDictionary(p => p.Key,
        p => new FunctionSignature(p.Key, p.Value.Parameters.Length, p.Value.Source), StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyDictionary<string, MacroDefinition> Definitions => new ReadOnlyDictionary<string, MacroDefinition>(macros);
    private AdspLibrary(string hash) { Sha256 = hash; }

    public static AdspLibrary Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 1_048_576) throw new InvalidDataException(DSNText.Russian("Execution.AdspLibrary.Text001"));
        var library = Parse(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'), path);
        library.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        return library;
    }
    public static AdspLibrary ReadMany(IReadOnlyList<string> paths)
    {
        if (paths.Count is < 1 or > 8) throw new ArgumentException(DSNText.Russian("Execution.AdspLibrary.Text002"));
        if (paths.Count == 1) return Read(paths[0]);
        var result = new AdspLibrary("");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in paths)
        {
            var library = Read(path); hash.AppendData(Convert.FromHexString(library.Sha256));
            foreach (var (name, definition) in library.macros)
                if (!result.macros.TryAdd(name, definition)) throw new AdspException(definition.Source, DSNText.Concat(DSNText.Russian("Execution.AdspLibrary.Text003"), name));
            result.Metadata = FunctionSemantics.Merge(result.Metadata, library.Metadata);
        }
        result.Sha256 = Convert.ToHexString(hash.GetHashAndReset()); return result;
    }

    public static AdspLibrary Parse(string text, string sourceName = "ADSP21.INC")
    {
        if (text.Length > 1_048_576) throw new InvalidDataException(DSNText.Russian("Execution.AdspLibrary.Text004"));
        var library = new AdspLibrary(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
        string? name = null; string[] parameters = []; SourceLocation? start = null;
        var body = new List<Statement>();
        foreach (var statement in Tokenize(text, sourceName))
        {
            var t = statement.Tokens;
            if (t[0].Text.Equals(".MACRO", StringComparison.OrdinalIgnoreCase))
            {
                if (name is not null) throw new AdspException(statement.Source, DSNText.Russian("Execution.AdspLibrary.Text005"));
                if (t.Count < 4 || !Identifier(t[1].Text) || t[2].Text != "(" || t[^1].Text != ")")
                    throw new AdspException(statement.Source, DSNText.Russian("Execution.AdspLibrary.Text006"));
                name = t[1].Text; start = statement.Source;
                var signature = t.Skip(3).Take(t.Count - 4).Select(x => x.Text).ToArray();
                parameters = signature.Where((_, i) => i % 2 == 0).ToArray();
                if (parameters.Length > 10 || signature.Where((_, i) => i % 2 == 1).Any(x => x != ",") ||
                    signature.Length != Math.Max(0, parameters.Length * 2 - 1) ||
                    !parameters.SequenceEqual(Enumerable.Range(0, parameters.Length).Select(i => DSNText.Concat("%", i))))
                    throw new AdspException(statement.Source, DSNText.Russian("Execution.AdspLibrary.Text007"));
                body.Clear();
            }
            else if (t[0].Text.Equals(".ENDMACRO", StringComparison.OrdinalIgnoreCase))
            {
                if (name is null || t.Count != 1) throw new AdspException(statement.Source, DSNText.Russian("Execution.AdspLibrary.Text008"));
                if (!library.macros.TryAdd(name, new MacroDefinition(name, parameters, body.ToArray(), start!)))
                    throw new AdspException(statement.Source, DSNText.Concat(DSNText.Russian("Execution.AdspLibrary.Text009"), name));
                if (library.macros.Count > 256) throw new AdspException(statement.Source, DSNText.Russian("Execution.AdspLibrary.Text010"));
                name = null;
            }
            else
            {
                if (name is null) throw new AdspException(statement.Source, DSNText.Russian("Execution.AdspLibrary.Text011"));
                body.Add(statement);
            }
        }
        if (name is not null) throw new AdspException(start!, DSNText.Concat(DSNText.Russian("Execution.AdspLibrary.Text012"), name));
        if (library.macros.Count == 0) throw new AdspException(new(sourceName, 1), DSNText.Russian("Execution.AdspLibrary.Text013"));
        library.Metadata = FunctionSemantics.ParseAnnotations(text, sourceName, library.Signatures);
        return library;
    }

    internal static bool Identifier(string value) => Regex.IsMatch(value, @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant);

    internal static IReadOnlyList<Statement> Tokenize(string text, string file)
    {
        var statements = new List<Statement>(); var tokens = new List<Token>();
        var line = 1; var comment = false; var commentLine = 0;
        for (var p = 0; p < text.Length;)
        {
            var c = text[p];
            if (c == '\n') { line++; p++; continue; }
            if (comment) { if (c == '}') comment = false; else if (c == '{') throw new AdspException(new(file, line), DSNText.Russian("Execution.AdspLibrary.Text014")); p++; continue; }
            if (c == '{') { comment = true; commentLine = line; p++; continue; }
            if (char.IsWhiteSpace(c)) { p++; continue; }
            if (c == ';')
            {
                if (tokens.Count != 0) statements.Add(new(tokens.ToArray(), tokens[0].Source));
                tokens.Clear(); p++; continue;
            }
            var begin = p;
            if (char.IsAsciiLetter(c) || c is '_' or '.' or '%')
            {
                p++;
                while (p < text.Length && (char.IsAsciiLetterOrDigit(text[p]) || text[p] == '_')) p++;
                if (p < text.Length && text[p] == '#' && text[begin..p].Equals("B", StringComparison.OrdinalIgnoreCase))
                { p++; while (p < text.Length && char.IsAsciiDigit(text[p])) p++; }
            }
            else if (char.IsAsciiDigit(c))
            { p++; while (p < text.Length && char.IsAsciiLetterOrDigit(text[p])) p++; }
            else if ("(),:=/+-^".Contains(c)) p++;
            else throw new AdspException(new(file, line), DSNText.Russian("Execution.AdspLibrary.Text015", c));
            tokens.Add(new(text[begin..p].ToUpperInvariant(), new(file, line)));
            if (tokens.Count > 256 || statements.Count > 20_000) throw new AdspException(new(file, line), DSNText.Russian("Execution.AdspLibrary.Text016"));
        }
        if (comment) throw new AdspException(new(file, commentLine), DSNText.Russian("Execution.AdspLibrary.Text017"));
        if (tokens.Count > 0) throw new AdspException(tokens[0].Source, DSNText.Russian("Execution.AdspLibrary.Text018"));
        return statements;
    }
}
