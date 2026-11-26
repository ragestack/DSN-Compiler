// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Text.Json.Serialization;
using DSN.Core;

namespace DSN.Execution;

public sealed class ProcessorHistory
{
    [JsonRequired] public string Architecture { get; set; } = "";
    [JsonRequired] public int RegisterBits { get; set; }
    [JsonRequired] public string[] Names { get; set; } = [];
    [JsonRequired] public long FirstTick { get; set; }
    [JsonRequired] public uint[][] Values { get; set; } = [];
    [JsonIgnore] public long LastTick => FirstTick + Values.Length - 1;
    public ProcessorHistory Copy() => new() { Architecture = Architecture, RegisterBits = RegisterBits,
        Names = (string[])Names.Clone(), FirstTick = FirstTick, Values = Values.Select(v => (uint[])v.Clone()).ToArray() };

    internal static void Bounds(ProcessorHistory? history, long lastTick)
    {
        if (history is null) return;
        if (history.Architecture is not ("ADSP21" or "RV32I" or "MSP430") || history.RegisterBits != (history.Architecture == "RV32I" ? 32 : 16) ||
            history.Names is null || history.Names.Length is < 1 or > 32 || history.Names.Any(n => n is null || !AdspLibrary.Identifier(n)) ||
            history.Names.Distinct(StringComparer.Ordinal).Count() != history.Names.Length || history.Values is null ||
            history.Values.Length is < 1 or > ExecutionSession.MaxTicks + 1 || history.FirstTick < 0 || history.FirstTick > lastTick || history.LastTick != lastTick ||
            history.Values.Any(v => v is null || v.Length != history.Names.Length || history.RegisterBits == 16 && v.Any(x => x > ushort.MaxValue)))
            throw new InvalidDataException(DSNText.Russian("Processor.Invalid"));
        if (history.Architecture == "RV32I" && (history.Names[0] != "X0" || history.Values.Any(v => v[0] != 0)))
            throw new InvalidDataException(DSNText.Russian("Processor.Invalid"));
    }
}

public sealed partial class ExecutionSession
{
    private readonly List<uint[]> processorRows = [];
    private long processorFirstTick;
    public ProcessorHistory CaptureProcessorHistory() => new() { Architecture = Architecture,
        RegisterBits = Architecture == "RV32I" ? 32 : 16, Names = Bound.Program.RegisterNames.ToArray(),
        FirstTick = processorFirstTick, Values = processorRows.Select(v => (uint[])v.Clone()).ToArray() };

    private ProcessorHistory PrepareProcessorRestore(SavedExecutionSession saved, IExecutionMachine initial)
    {
        ProcessorHistory.Bounds(saved.ProcessorHistory, saved.Machine.Tick);
        var names = Bound.Program.RegisterNames.ToArray();
        var history = saved.ProcessorHistory?.Copy() ?? new ProcessorHistory { Architecture = Architecture,
            RegisterBits = Architecture == "RV32I" ? 32 : 16, Names = names, FirstTick = saved.Machine.Tick,
            Values = [names.Select(n => saved.Machine.Registers[n]).ToArray()] };
        ProcessorHistory.Bounds(history, saved.Machine.Tick);
        if (history.Architecture != Architecture || !history.Names.SequenceEqual(names) ||
            !history.Values[^1].SequenceEqual(names.Select(n => saved.Machine.Registers[n])) ||
            history.FirstTick == 0 && !history.Values[0].SequenceEqual(names.Select(n => initial.Registers[n])))
            throw new InvalidDataException(DSNText.Russian("Processor.Invalid"));
        if (Architecture == "MSP430")
            for (var i = 0; i < history.Values.Length; i++)
            {
                var status = history.Values[i][2];
                if (saved.History[checked((int)history.FirstTick + i)].Flags != new AdspFlags((status & 2) != 0, (status & 4) != 0, (status & 256) != 0, (status & 1) != 0))
                    throw new InvalidDataException(DSNText.Russian("Processor.Invalid"));
            }
        return history;
    }
}
