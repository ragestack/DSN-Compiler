// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSN.Execution;

public sealed record FunctionSignature(string Name, int Parameters, SourceLocation Source);
public sealed record ExecutionBlockInfo(string Instance, string Macro);

public interface IFunctionLibrary
{
    string Sha256 { get; }
    IReadOnlyDictionary<string, FunctionSignature> Signatures { get; }
    FunctionSemantics? Metadata => null;
}

public interface IExecutionProgram
{
    string Architecture { get; }
    string KernelId { get; }
    string LibrarySha256 { get; }
    IReadOnlyDictionary<string, int> Addresses { get; }
    ushort[] InitialImage { get; }
    IReadOnlyList<string> RegisterNames { get; }
    IReadOnlyList<string> InstanceOrder { get; }
    IReadOnlyList<ExecutionBlockInfo> BlockInfo { get; }
    SourceLocation SourceForInstance(string instance);
    IExecutionMachine CreateMachine();
}

public interface IExecutionMachine
{
    long Tick { get; }
    bool Faulted { get; }
    AdspFlags Flags { get; }
    IReadOnlyDictionary<string, uint> Registers { get; }
    ushort ReadWord(string name);
    void WriteWord(string name, ushort value);
    IReadOnlyDictionary<string, ushort> Snapshot();
    AdspMachineState CaptureState();
    void RestoreState(AdspMachineState state);
    StepExecutionRecord Step(IReadOnlyDictionary<string, ushort>? inputs = null, int instructionBudget = 100_000);
}

public static class ExecutionCompiler
{
    public static IExecutionProgram Compile(IFunctionLibrary library, IReadOnlyList<MacroInvocation> calls,
        IReadOnlyDictionary<string, ushort> words, IReadOnlyDictionary<string, int>? constants = null,
        IReadOnlyDictionary<string, string>? aliases = null) => library switch
        {
            AdspLibrary adsp => AdspProgram.Compile(adsp, calls, words, constants, aliases),
            AssemblyLibrary assembly => AssemblyProgram.Compile(assembly, calls, words, constants, aliases),
            _ => throw new ArgumentException(DSNText.Russian("Execution.ExecutionBackend.Text001"))
        };
}

public sealed class ExecutionImplementation
{
    public const string StandardLibrary = "LOGIC";
    private static readonly string[] supported = ["ADSP21", "RV32I", "MSP430"];
    public string Id { get; set; } = "";
    public string Architecture { get; set; } = "";
    public int Version { get; set; }
    public Dictionary<string, string> Libraries { get; set; } = [];

    private static ExecutionImplementation Read(string assetsRoot, string implementation)
    {
        var manifestPath = Path.Combine(assetsRoot, "implementations", DSNText.Concat(ExecutionSession.SafeName(implementation), ".json"));
        var manifest = DSNProgramBinder.ReadJson<ExecutionImplementation>(manifestPath);
        if (manifest.Id != implementation || manifest.Version != 1 || manifest.Architecture is not ("ADSP21" or "RV32I" or "MSP430") || manifest.Libraries is null)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionBackend.Text002"));
        return manifest;
    }

    private static string[] Paths(string assetsRoot, ExecutionProfile profile, ExecutionImplementation manifest)
    {
        if (profile.Libraries is null || profile.Libraries.Length is < 1 or > 8)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionBackend.Text003"));
        return profile.Libraries.Select(name => Path.Combine(assetsRoot, "libraries", ExecutionSession.SafeName(
            manifest.Libraries.TryGetValue(name, out var mapped) ? mapped : throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionBackend.Text004"), name))))).ToArray();
    }

    // Наличие реализации определяется её описанием и всеми запрошенными файлами INC.
    // Разбор выбранного кода и проверка его контракта остаются в обычном пути загрузки.
    public static string[] Available(string assetsRoot, ExecutionProfile? profile = null)
    {
        profile ??= UserProgramPreparation.StandardScope();
        var result = new List<string>();
        foreach (var id in supported)
        {
            try
            {
                var manifest = Read(assetsRoot, id);
                if (Paths(assetsRoot, profile, manifest).All(File.Exists)) result.Add(id);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or JsonException) { }
        }
        return result.ToArray();
    }

    public static string Select(string assetsRoot, ExecutionProfile? profile = null, string? implementation = null)
    {
        if (!string.IsNullOrEmpty(implementation)) return implementation;
        return Available(assetsRoot, profile).FirstOrDefault()
            ?? throw new InvalidOperationException(DSNText.Russian("Implementation.Connect"));
    }

    public static string ForIdentity(string assetsRoot, ExecutionProfile profile, ScenarioIdentity identity, string? implementation = null)
    {
        if (!string.IsNullOrEmpty(implementation)) return implementation;
        foreach (var id in Available(assetsRoot, profile))
            if (Load(assetsRoot, profile, id).Sha256 == identity.LibrarySha256) return id;
        throw new InvalidDataException(DSNText.Russian("Implementation.Record"));
    }

    public static IFunctionLibrary Load(string assetsRoot, ExecutionProfile profile, string? implementation = null)
    {
        var id = Select(assetsRoot, profile, implementation);
        var manifest = Read(assetsRoot, id);
        var paths = Paths(assetsRoot, profile, manifest);
        if (paths.Any(path => !File.Exists(path))) throw new FileNotFoundException(DSNText.Russian("Implementation.Unavailable", id));
        return manifest.Architecture switch
        {
            "ADSP21" => AdspLibrary.ReadMany(paths),
            _ => AssemblyLibrary.ReadMany(paths, manifest.Architecture)
        };
    }
}
