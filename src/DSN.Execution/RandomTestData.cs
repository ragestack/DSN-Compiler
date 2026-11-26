// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DSN.Core;

namespace DSN.Execution;

public sealed record TestDataRange(int Minimum, int Maximum);
public sealed record RandomTestSettings(uint Seed, TestDataRange Word, TestDataRange Mask, TestDataRange Timer, TestDataRange Selection)
{
    public static RandomTestSettings Default => new(1, new(0, 255), new(1, 65535), new(0, 60), new(0, 2));
}
public sealed record RandomTestBatch(string Algorithm, RandomTestSettings Settings, Dictionary<string, int> GeneratedValues);
public sealed record TestDataRangeDraft(string Minimum, string Maximum);
public sealed record RandomTestDraft(string Seed, Dictionary<string, TestDataRangeDraft> Ranges);

public static class RandomTestData
{
    public const string Algorithm = "sha256-per-parameter-v1";
    public static void Validate(RandomTestSettings settings)
    {
        if (settings is null) throw new InvalidDataException(DSNText.Russian("Random.InvalidSettings"));
        foreach (var (range, minimum, maximum) in new[] { (settings.Word, -32768, 65535), (settings.Mask, 0, 65535), (settings.Timer, 0, 32767), (settings.Selection, 0, 65535) })
            if (range is null || range.Minimum < minimum || range.Maximum > maximum || range.Minimum > range.Maximum)
                throw new InvalidDataException(DSNText.Russian("Random.InvalidRanges"));
    }
    public static void Validate(RandomTestBatch[]? batches)
    {
        if (batches is null) return;
        if (batches.Length > 128) throw new InvalidDataException(DSNText.Russian("Random.TooManyBatches"));
        foreach (var batch in batches)
        {
            if (batch is null || batch.Algorithm != Algorithm || batch.GeneratedValues is null || batch.GeneratedValues.Count is < 1 or > 10000 ||
                batch.GeneratedValues.Any(p => p.Key.Length > 512 || !(p.Key.StartsWith("constant:", StringComparison.Ordinal) || p.Key.StartsWith("environment:", StringComparison.Ordinal)) || p.Value is < -32768 or > 65535))
                throw new InvalidDataException(DSNText.Russian("Random.InvalidProvenance"));
            Validate(batch.Settings);
        }
    }
    public static void Validate(RandomTestDraft? draft)
    {
        if (draft is null) return;
        if (draft.Seed is null || draft.Seed.Length > 128 || draft.Ranges is null || draft.Ranges.Count != 4 ||
            new[] { "Word", "Mask", "Timer", "Selection" }.Any(k => !draft.Ranges.ContainsKey(k)) ||
            draft.Ranges.Values.Any(r => r is null || r.Minimum is null || r.Maximum is null || r.Minimum.Length > 128 || r.Maximum.Length > 128))
            throw new InvalidDataException(DSNText.Russian("Random.InvalidSettings"));
    }
    public static RandomTestBatch Generate(string dsnSha256, IEnumerable<DataParameter> parameters, VariableAnalysis analysis,
        IReadOnlyDictionary<string, string> drafts, RandomTestSettings settings)
    {
        Validate(settings);
        if (dsnSha256.Length != 64 || !dsnSha256.All(Uri.IsHexDigit) || analysis.DSNSha256 != dsnSha256 || !analysis.VerifiedLibrary ||
            analysis.Issues.Any(i => i.Severity == "ERROR" && i.Code is not ("CONSTANT_MISSING" or "ENVIRONMENT_MISSING")))
            throw new InvalidDataException(DSNText.Russian("Random.AnalysisRequired"));
        var variables = analysis.Variables.SelectMany(v => v.Evidence.Select(e => (e.Endpoint, Variable: v))).GroupBy(p => p.Endpoint)
            .ToDictionary(g => g.Key, g => g.First().Variable, StringComparer.Ordinal);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var parameter in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (parameter.ExistingValue is not null || !string.IsNullOrWhiteSpace(drafts.GetValueOrDefault(parameter.Key))) continue;
            var minimum = parameter.Kind == "environment" ? 0 : -32768; var maximum = 65535;
            var ranges = new List<TestDataRange>();
            if (parameter.Kind == "constant" && parameter.Ports.FirstOrDefault() is { } port && variables.TryGetValue(port, out var variable))
            {
                if (variable.Roles.Contains("mask")) ranges.Add(settings.Mask);
                if (variable.Roles.Any(r => r is "tick-setting" or "initial-setting" or "counter")) ranges.Add(settings.Timer);
                if (variable.Roles.Any(r => r is "selection" or "selection-default")) ranges.Add(settings.Selection);
                if (variable.ExpectedType == "boolean") ranges.Add(new(0, 1));
            }
            if (ranges.Count == 0) ranges.Add(settings.Word);
            foreach (var range in ranges) { minimum = Math.Max(minimum, range.Minimum); maximum = Math.Min(maximum, range.Maximum); }
            if (minimum > maximum) throw new InvalidDataException(DSNText.Russian("Random.NoIntersection", parameter.Name));
            values.Add(parameter.Key, Draw(dsnSha256, parameter.Key, settings.Seed, minimum, maximum));
        }
        return new(Algorithm, settings, values);
    }
    private static int Draw(string hash, string key, uint seed, int minimum, int maximum)
    {
        var span = (ulong)((long)maximum - minimum + 1);
        var threshold = unchecked(0UL - span) % span;
        for (uint attempt = 0; ; attempt++)
        {
            var text = string.Join("\0", Algorithm, hash, seed.ToString(CultureInfo.InvariantCulture), key, attempt.ToString(CultureInfo.InvariantCulture));
            var value = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
            if (value >= threshold) return (int)((long)minimum + (long)(value % span));
        }
    }
}
