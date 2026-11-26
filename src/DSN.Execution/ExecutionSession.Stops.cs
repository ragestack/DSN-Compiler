// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Text.Json.Serialization;

namespace DSN.Execution;

public enum StopComparison { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual, Changed, MaskSet, MaskClear }
public sealed record StopCondition(
    [property: JsonRequired] bool Enabled, [property: JsonRequired] string Word,
    [property: JsonRequired] StopComparison Comparison, [property: JsonRequired] ushort Value);
public sealed record StopMatch([property: JsonRequired] int Index, [property: JsonRequired] string Word,
    [property: JsonRequired] StopComparison Comparison, [property: JsonRequired] ushort Expected,
    [property: JsonRequired] ushort Before, [property: JsonRequired] ushort After);
public sealed record StopHit([property: JsonRequired] long Tick, [property: JsonRequired] IReadOnlyList<StopMatch> Matches);
public sealed class ExecutionStopState
{
    [JsonRequired] public StopCondition[] Conditions { get; set; } = [];
    [JsonRequired] public StopHit? LastHit { get; set; }
}
public sealed record StopConditionDraft([property: JsonRequired] bool Enabled, [property: JsonRequired] string Word,
    [property: JsonRequired] StopComparison Comparison, [property: JsonRequired] string Value);

public sealed partial class ExecutionSession
{
    public const int MaxStopConditions = 128;
    private StopCondition[] stopConditions = [];
    public IReadOnlyList<StopCondition> StopConditions => Array.AsReadOnly(stopConditions);
    public StopHit? LastStop { get; private set; }

    public void ConfigureStops(IEnumerable<StopCondition> conditions)
    {
        var validated = ValidateStops(conditions);
        stopConditions = validated; LastStop = null;
    }

    public StopHit? AdvanceUntilStop(int count = 1)
    {
        if (count < 1 || Tick + count > TickLimit) throw new InvalidOperationException(DSNText.Russian("Execution.ExecutionSession.Text008", TickLimit));
        for (var i = 0; i < count; i++)
        {
            Step();
            LastStop = EvaluateStops(stopConditions, samples[^2], samples[^1]);
            if (LastStop is not null) break;
        }
        return LastStop;
    }

    private StopCondition[] ValidateStops(IEnumerable<StopCondition>? conditions)
    {
        if (conditions is null) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text001"));
        var result = conditions.Take(MaxStopConditions + 1).ToArray();
        if (result.Length > MaxStopConditions) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text002", MaxStopConditions));
        for (var i = 0; i < result.Length; i++)
        {
            var rule = result[i];
            if (rule is null || !Variables.Contains(rule.Word, StringComparer.OrdinalIgnoreCase) || !Enum.IsDefined(rule.Comparison) ||
                rule.Comparison is StopComparison.MaskSet or StopComparison.MaskClear && rule.Value == 0)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text003", i + 1));
        }
        return result;
    }

    private static StopHit? EvaluateStops(IReadOnlyList<StopCondition> conditions, StateSample before, StateSample after)
    {
        var matches = new List<StopMatch>();
        for (var i = 0; i < conditions.Count; i++)
        {
            var rule = conditions[i]; if (!rule.Enabled) continue;
            var previous = before.Words[rule.Word]; var current = after.Words[rule.Word];
            var matched = rule.Comparison switch
            {
                StopComparison.Equal => current == rule.Value,
                StopComparison.NotEqual => current != rule.Value,
                StopComparison.Less => current < rule.Value,
                StopComparison.LessOrEqual => current <= rule.Value,
                StopComparison.Greater => current > rule.Value,
                StopComparison.GreaterOrEqual => current >= rule.Value,
                StopComparison.Changed => current != previous,
                StopComparison.MaskSet => (current & rule.Value) != 0,
                StopComparison.MaskClear => (current & rule.Value) == 0,
                _ => throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text004"))
            };
            if (matched) matches.Add(new(i, rule.Word, rule.Comparison, rule.Value, previous, current));
        }
        return matches.Count == 0 ? null : new(after.Tick, matches.AsReadOnly());
    }

    private ExecutionStopState? CaptureStops() => stopConditions.Length == 0 ? null : new() { Conditions = stopConditions.ToArray(), LastHit = LastStop };
    private ExecutionStopState? PrepareStops(ExecutionStopState? state, IReadOnlyList<StateSample> history)
    {
        if (state is null) return null;
        var conditions = ValidateStops(state.Conditions);
        StopHit? hit = null;
        if (state.LastHit is { } saved)
        {
            if (history.Count < 2 || saved.Tick != history[^1].Tick || saved.Matches is null)
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text005"));
            hit = EvaluateStops(conditions, history[^2], history[^1]);
            if (hit is null || !hit.Matches.SequenceEqual(saved.Matches))
                throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text006"));
        }
        return new() { Conditions = conditions, LastHit = hit };
    }

    public static string StopOperationText(StopComparison operation) => operation switch
    {
        StopComparison.Equal => "=", StopComparison.NotEqual => "≠", StopComparison.Less => "<",
        StopComparison.LessOrEqual => "≤", StopComparison.Greater => ">", StopComparison.GreaterOrEqual => "≥",
        StopComparison.Changed => DSNText.Russian("Execution.ExecutionSession.Stops.Text007"), StopComparison.MaskSet => DSNText.Russian("Execution.ExecutionSession.Stops.Text008"), StopComparison.MaskClear => DSNText.Russian("Execution.ExecutionSession.Stops.Text009"),
        _ => throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSession.Stops.Text004"))
    };
    public static string StopMatchText(StopMatch match) => DSNText.Concat(DSNText.Concat(DSNText.Compose($"№{match.Index + 1}: {match.Word} {StopOperationText(match.Comparison)}"), (match.Comparison == StopComparison.Changed ? "" : DSNText.Concat(" ", FormatWord(match.Expected)))), DSNText.Compose($" · {FormatWord(match.Before)} → {FormatWord(match.After)}"));
}
