// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed record VariableTimelineBucket(int FirstTick, int LastTick, ushort First, ushort Last, ushort Minimum, ushort Maximum);

internal static class VariableTimelineData
{
    public static IReadOnlyList<VariableTimelineBucket> Buckets(IReadOnlyList<StateSample> samples, string name, int start, int end, int columns)
    {
        if (start < 0 || end < start || end >= samples.Count || columns < 1) throw new ArgumentOutOfRangeException(nameof(start));
        var count = end - start + 1; var buckets = Math.Min(count, columns); var result = new VariableTimelineBucket[buckets];
        for (var i = 0; i < buckets; i++)
        {
            var firstTick = start + (int)((long)i * count / buckets);
            var lastTick = start + (int)((long)(i + 1) * count / buckets) - 1;
            var first = Value(samples, name, firstTick); var last = first; var min = first; var max = first;
            for (var tick = firstTick + 1; tick <= lastTick; tick++)
            {
                last = Value(samples, name, tick); min = Math.Min(min, last); max = Math.Max(max, last);
            }
            result[i] = new(firstTick, lastTick, first, last, min, max);
        }
        return result;
    }

    public static ushort Value(IReadOnlyList<StateSample> samples, string name, int tick)
    {
        if (tick < 0 || tick >= samples.Count || samples[tick].Tick != tick) throw new ArgumentOutOfRangeException(nameof(tick));
        return samples[tick].Words[name];
    }
    public static int TickAt(double x, double left, double width, int start, int end) =>
        Math.Clamp(start + (int)Math.Round(Math.Clamp((x - left) / Math.Max(1, width), 0, 1) * (end - start)), start, end);
}
