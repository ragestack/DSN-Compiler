// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Globalization;

namespace DSN.Core;

public sealed record ResolvedPropertyLabel(DisplayProperty Layout, string Name, string Text);

public static class PropertyDisplay
{
    public static IReadOnlyList<DisplayProperty> Labels(Symbol symbol, Instance instance)
    {
        var labels = new Dictionary<uint, DisplayProperty>();
        foreach (var label in symbol.Labels) labels[label.Index] = label;
        foreach (var label in instance.Labels) labels[label.Index] = label;
        return labels.Values.ToArray();
    }

    public static string Value(LogicProgramDocument document, Page page, Symbol symbol, Instance instance, uint index)
    {
        var name = document.String(index);
        if (name == "Part Reference") return instance.Reference;
        if (name is "Value" or "Name") return instance.Value;

        if (instance.Properties.TryGetValue((ushort)index, out var value)) return document.String(value);
        if (name == "Page Modify Date")
            return page.ModifiedTimestamp == 0 ? "" : DateTimeOffset.FromUnixTimeSeconds(page.ModifiedTimestamp)
                .ToString("dddd, MMMM dd, yyyy", CultureInfo.GetCultureInfo("en-US"));
        return symbol.DefaultProperties.TryGetValue((ushort)index, out value) ? document.String(value) : "";
    }

    public static IEnumerable<ResolvedPropertyLabel> Resolve(LogicProgramDocument document, Page page, Symbol symbol, Instance instance)
    {
        foreach (var label in Labels(symbol, instance))
        {
            var mode = label.Display >> 8;
            var name = document.String(label.Index);
            var value = mode == 0 ? "" : mode == 3 ? name : Value(document, page, symbol, instance, label.Index);
            if (mode is 2 or 4 && value.Length > 0) value = DSNText.Concat(DSNText.Concat(name, "="), value);
            yield return new(label, name, value);
        }
    }
}
