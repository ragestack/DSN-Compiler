// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Text.Json;

namespace DSN.Core;

public sealed record LogicProgramModelSnapshot(string Format, int Version, LogicProgramDocument Program);

public static class LogicProgramModelFile
{
    public static string Serialize(LogicProgramDocument document) => JsonSerializer.Serialize(
        new LogicProgramModelSnapshot("DSNCompiler.ProgramModel", 2, document),
        new JsonSerializerOptions { WriteIndented = true });
}
