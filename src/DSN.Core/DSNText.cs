// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DSN.Core;

// Канонический текст остаётся прежним.
public static class DSNText
{
    private sealed record Message(string? Key, string? Format, object?[] Arguments);
    private static readonly ConditionalWeakTable<string, Message> messages = new();
    private static readonly ConcurrentDictionary<string, string> constants = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> baseRussian = Embedded("ru");
    private static readonly Dictionary<string, string> source = Canonical();
    private static readonly Dictionary<string, string> english = Embedded("en");
    private static Dictionary<string, string> russian = baseRussian;
    private static Dictionary<string, string> translatedEnglish = english;
    public static string Language { get; private set; } = "ru";
    public static event Action? LanguageChanged;
    public static IReadOnlyDictionary<string, string> RussianCatalog => source;
    public static IReadOnlyDictionary<string, string> EnglishCatalog => english;
    // Для представления используем переводы; канонический каталог сохраняет машинные данные.
    public static IReadOnlyDictionary<string, string> TranslationCatalog(string language) => language switch
    {
        "ru" => russian,
        "en" => translatedEnglish,
        _ => throw new ArgumentException(Russian("Localization.LanguageArgument"), nameof(language))
    };
    public static string? CatalogDiagnostic { get; private set; }

    private static Dictionary<string, string> Embedded(string language)
    {
        using var stream = typeof(DSNText).Assembly.GetManifestResourceStream("DSN.Languages." + language + ".json")
            ?? throw new InvalidDataException("Missing embedded language: " + language);
        using var reader = new StreamReader(stream);
        return ReadCatalog(reader.ReadToEnd(), language);
    }
    private static Dictionary<string, string> Canonical()
    {
        var result = new Dictionary<string, string>(baseRussian, StringComparer.Ordinal);
        using var stream = typeof(DSNText).Assembly.GetManifestResourceStream("DSN.Languages.ru.json")!;
        using var document = JsonDocument.Parse(stream);
        // Закрепляем прежние машинные сообщения отдельно от редактируемых переводов.
        if (document.RootElement.TryGetProperty("Canonical", out var overrides))
            foreach (var entry in overrides.EnumerateObject()) result[entry.Name] = entry.Value.GetString()!;
        return result;
    }
    public static Dictionary<string, string> ReadCatalog(string json, string language)
    {
        if (json.Length > 1024 * 1024) throw new InvalidDataException("Language file exceeds 1 MiB.");
        using var document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        var root = document.RootElement;
        if (root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count())
            throw new InvalidDataException("Duplicate language header field.");
        if (root.GetProperty("Version").GetInt32() != 1 || root.GetProperty("Language").GetString() != language)
            throw new InvalidDataException("Invalid language catalog header.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in root.GetProperty("Strings").EnumerateObject())
            if (pair.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pair.Value.GetString()) || !result.TryAdd(pair.Name, pair.Value.GetString()!))
                throw new InvalidDataException("Invalid or duplicate language key: " + pair.Name);
        return result;
    }
    public static void ValidateCatalog(IReadOnlyDictionary<string, string> reference, IReadOnlyDictionary<string, string> candidate)
    {
        if (!reference.Keys.Order(StringComparer.Ordinal).SequenceEqual(candidate.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Language key sets differ.");
        foreach (var (key, template) in reference)
        {
            var original = System.Text.CompositeFormat.Parse(template); var translated = System.Text.CompositeFormat.Parse(candidate[key]);
            if (original.MinimumArgumentCount != translated.MinimumArgumentCount)
                throw new InvalidDataException("Language placeholders differ: " + key);
            var slots = System.Text.RegularExpressions.Regex.Matches(template, @"(?<!\{)\{([0-9]+)(?:[,}:])").Select(m => m.Groups[1].Value).Order().ToArray();
            var other = System.Text.RegularExpressions.Regex.Matches(candidate[key], @"(?<!\{)\{([0-9]+)(?:[,}:])").Select(m => m.Groups[1].Value).Order().ToArray();
            if (!slots.SequenceEqual(other)) throw new InvalidDataException("Language placeholder occurrences differ: " + key);
            // Переставлять аргументы можно; менять их формат/выравнивание переводом нельзя.
            const string token = @"(?<!\{)\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}";
            var formats = System.Text.RegularExpressions.Regex.Matches(template, token).Select(m => m.Value).Order().ToArray();
            var translatedFormats = System.Text.RegularExpressions.Regex.Matches(candidate[key], token).Select(m => m.Value).Order().ToArray();
            if (!formats.SequenceEqual(translatedFormats)) throw new InvalidDataException("Language placeholder formats differ: " + key);
        }
    }
    public static void Configure(string root)
    {
        russian = baseRussian; translatedEnglish = english; CatalogDiagnostic = null;
        foreach (var language in new[] { "ru", "en" })
        {
            var path = Path.Combine(root, "languages", language + ".json");
            if (!File.Exists(path)) continue;
            try
            {
                if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Language file exceeds 1 MiB.");
                var catalog = ReadCatalog(File.ReadAllText(path), language); ValidateCatalog(source, catalog);
                if (language == "ru") russian = catalog; else translatedEnglish = catalog;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or FormatException or KeyNotFoundException or InvalidOperationException)
            { CatalogDiagnostic = Russian("Localization.CatalogError", path, error.Message); }
        }
    }
    public static void SetLanguage(string language)
    {
        if (language is not ("ru" or "en")) throw new ArgumentException("Language must be ru or en.");
        if (Language == language) return;
        Language = language; LanguageChanged?.Invoke();
    }
    private static object? Freeze(object? value) => value is null or string or IFormattable ? value : value.ToString();
    private static string Track(string text, Message message)
    {
        var result = new string(text.ToCharArray()); messages.Add(result, message); return result;
    }
    public static string Russian(string key, params object?[] args)
    {
        if (!source.TryGetValue(key, out var template)) throw new KeyNotFoundException("Language key: " + key);
        if (args.Length == 0) return constants.GetOrAdd(key, _ => Track(template, new(key, null, [])));
        return Track(string.Format(CultureInfo.CurrentCulture, template, args), new(key, null, args.Select(Freeze).ToArray()));
    }
    public static string Compose(FormattableString value)
    {
        var args = value.GetArguments().Select(Freeze).ToArray(); var result = value.ToString(CultureInfo.CurrentCulture);
        return args.OfType<string>().Any(HasMessage) ? Track(result, new(null, value.Format, args)) : result;
    }
    private static bool HasMessage(string value) => messages.TryGetValue(value, out _);
    public static string Concat(object? left, object? right)
    {
        var a = Convert.ToString(left, CultureInfo.CurrentCulture) ?? ""; var b = Convert.ToString(right, CultureInfo.CurrentCulture) ?? "";
        var result = string.Concat(a, b);
        return HasMessage(a) || HasMessage(b) ? Track(result, new(null, null, [a, b])) : result;
    }
    public static string Join<T>(string? separator, IEnumerable<T> values)
    {
        var parts = values.Select(v => Convert.ToString(v, CultureInfo.CurrentCulture) ?? "").ToArray();
        var result = string.Join(separator, parts);
        if (!parts.Any(HasMessage) && (separator is null || !HasMessage(separator))) return result;
        var joined = new List<object?>();
        for (var i = 0; i < parts.Length; i++) { if (i > 0) joined.Add(separator ?? ""); joined.Add(parts[i]); }
        return Track(result, new(null, null, joined.ToArray()));
    }
    public static string Join<T>(char separator, IEnumerable<T> values) => Join(separator.ToString(), values);
    public static string Join(string? separator, params string?[] values) => Join<string?>(separator, values);
    public static string Join(char separator, params string?[] values) => Join<string?>(separator.ToString(), values);
    public static string Translate(string value) => messages.TryGetValue(value, out var message)
        ? Track(Render(value, 0), message) : value;
    private static string Render(string value, int depth)
    {
        if (depth > 256 || !messages.TryGetValue(value, out var message)) return value;
        var args = message.Arguments.Select(a => a is string s ? Render(s, depth + 1) : a).ToArray();
        var culture = CultureInfo.GetCultureInfo(Language == "en" ? "en-US" : "ru-RU");
        if (message.Key is { } key)
        {
            var catalog = Language == "en" ? translatedEnglish : russian;
            return string.Format(culture, catalog.GetValueOrDefault(key, source[key]), args);
        }
        return message.Format is { } format ? string.Format(culture, format, args) : string.Concat(args);
    }
}
