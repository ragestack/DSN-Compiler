// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Text;
using System.Text.Json;

namespace DSN.Execution;

internal static class ExecutionJson
{
    internal static T Parse<T>(string json, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(json) > maxBytes) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionJson.Text001"));
        using var document = JsonDocument.Parse(json);
        void Unique(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionJson.Text002"), property.Name));
                    Unique(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var child in element.EnumerateArray()) Unique(child);
        }
        Unique(document.RootElement);
        return document.RootElement.Deserialize<T>(DSNProgramBinder.JsonOptions) ?? throw new InvalidDataException(DSNText.Russian("Execution.ExecutionJson.Text003"));
    }
}
