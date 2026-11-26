// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Collections.ObjectModel;

namespace DSN.Execution;

internal sealed class AssemblyMachine : IExecutionMachine
{
    private readonly AssemblyProgram program;
    private ushort[] memory;
    private uint[] registers;
    public long Tick { get; private set; }
    public bool Faulted { get; private set; }
    public AdspFlags Flags => program.Architecture == "RV32I" ? new(false, false, false, false) :
        new((registers[2] & 2) != 0, (registers[2] & 4) != 0, (registers[2] & 256) != 0, (registers[2] & 1) != 0);
    public IReadOnlyDictionary<string, uint> Registers => program.RegisterNames.Select((n, i) => KeyValuePair.Create(n, registers[i])).ToDictionary();
    internal AssemblyMachine(AssemblyProgram program)
    { this.program = program; memory = program.InitialImage; registers = new uint[program.RegisterNames.Count]; }
    public ushort ReadWord(string name) => memory[Resolve(name)];
    public void WriteWord(string name, ushort value) => memory[Resolve(name)] = value;
    private int Resolve(string name) => program.Addresses.TryGetValue(name, out var index) ? index : throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text043"), name));
    public IReadOnlyDictionary<string, ushort> Snapshot() => new ReadOnlyDictionary<string, ushort>(program.Addresses.ToDictionary(p => p.Key, p => memory[p.Value], StringComparer.OrdinalIgnoreCase));
    public AdspMachineState CaptureState()
    {
        if (Faulted) throw new InvalidOperationException(DSNText.Russian("Execution.AssemblyMachine.Text001"));
        return new() { Architecture = program.Architecture, Tick = Tick, MemoryWords = memory.Length, Memory = (ushort[])memory.Clone(), Registers = new(Registers), Flags = Flags };
    }
    public void RestoreState(AdspMachineState state)
    {
        if (state is null || state.Architecture != program.Architecture || state.Tick < 0 || state.Memory is null || state.Memory.Length != memory.Length ||
            state.MemoryWords != memory.Length || state.Registers is null || state.Flags is null || state.Registers.Count != registers.Length ||
            !state.Registers.Keys.Order(StringComparer.Ordinal).SequenceEqual(program.RegisterNames.Order(StringComparer.Ordinal)))
            throw new InvalidDataException(DSNText.Russian("Execution.AssemblyMachine.Text002"));
        var values = program.RegisterNames.Select(n => state.Registers[n]).ToArray();
        if (program.Architecture == "RV32I")
        {
            if (values[0] != 0 || state.Flags != new AdspFlags(false, false, false, false)) throw new InvalidDataException(DSNText.Russian("Execution.AssemblyMachine.Text003"));
        }
        else
        {
            if (values.Any(v => v > 65535) || values[0] != 0 || values[1] != 0 || values[3] != 0 || (values[2] & ~0x107u) != 0 ||
                state.Flags != new AdspFlags((values[2] & 2) != 0, (values[2] & 4) != 0, (values[2] & 256) != 0, (values[2] & 1) != 0))
                throw new InvalidDataException(DSNText.Russian("Execution.AssemblyMachine.Text004"));
        }
        memory = (ushort[])state.Memory.Clone(); registers = values; Tick = state.Tick; Faulted = false;
    }
    private int Address(CpuOperand operand, SourceLocation source)
    {
        var address = operand.Indirect ? (long)unchecked(registers[operand.Register] + (uint)operand.Value) : operand.Value;
        if (program.Architecture == "MSP430") address &= 0xffff;
        if (address < 0 || address % 2 != 0 || address / 2 >= memory.Length) throw new AdspException(source, DSNText.Russian("Execution.AssemblyMachine.Text005"));
        return (int)(address / 2);
    }
    private uint Read(CpuOperand operand, SourceLocation source) => operand.Memory ? memory[Address(operand, source)] :
        operand.Register >= 0 ? registers[operand.Register] : unchecked((uint)operand.Value);
    private void Write(CpuOperand operand, uint value, SourceLocation source, List<MemoryChange> changes)
    {
        if (operand.Memory)
        { var address = Address(operand, source); changes.Add(new(program.Name(address), memory[address], (ushort)value)); memory[address] = (ushort)value; }
        else if (program.Architecture != "RV32I" || operand.Register != 0)
        {
            if (program.Architecture == "MSP430") value &= 65535;
            if (program.Architecture == "MSP430" && operand.Register == 2 && (value & ~0x107u) != 0)
                throw new AdspException(source, DSNText.Russian("Execution.AssemblyMachine.Text006"));
            registers[operand.Register] = value;
        }
    }
    public StepExecutionRecord Step(IReadOnlyDictionary<string, ushort>? inputs = null, int instructionBudget = 100_000)
    {
        if (Faulted) throw new InvalidOperationException(DSNText.Russian("Execution.AdspProgram.Text046"));
        if (instructionBudget is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        var assignments = inputs?.Select(p => (Index: Resolve(p.Key), p.Value)).ToArray() ?? [];
        foreach (var (index, value) in assignments) memory[index] = value;
        var records = new List<FunctionExecutionRecord>(); var total = 0;
        try
        {
            foreach (var block in program.Blocks)
            {
                var pc = 0; var executed = 0; var changes = new List<MemoryChange>();
                while (pc < block.Code.Length)
                {
                    var instruction = block.Code[pc++];
                    if (++total > instructionBudget) throw new AdspException(instruction.Source, DSNText.Russian("Execution.AdspProgram.Text047"));
                    executed++;
                    if (program.Architecture == "RV32I") Risc(instruction, ref pc, changes); else Msp(instruction, ref pc, changes);
                }
                records.Add(new(block.Instance, block.Macro, executed, changes));
            }
            return new(++Tick, records, Flags);
        }
        catch { Faulted = true; throw; }
    }
    private void Risc(CpuInstruction instruction, ref int pc, List<MemoryChange> changes)
    {
        var operands = instruction.Args; var s = instruction.Source; var op = instruction.Op;
        uint R(int i) => Read(operands[i], s);
        if (op == "j") { pc = operands[0].Value; return; }
        if (op is "beq" or "bne" or "blt" or "bge" or "bltu" or "bgeu")
        {
            var a = R(0); var b = R(1);
            var take = op switch { "beq" => a == b, "bne" => a != b, "blt" => (int)a < (int)b,
                "bge" => (int)a >= (int)b, "bltu" => a < b, _ => a >= b };
            if (take) pc = operands[2].Value; return;
        }
        if (op == "sh") { Write(operands[1], R(0), s, changes); return; }
        if (op is "lhu" or "lh")
        {
            var word = memory[Address(operands[1], s)]; Write(operands[0], op == "lh" ? unchecked((uint)(short)word) : word, s, changes); return;
        }
        var left = R(1); var right = operands.Length > 2 ? R(2) : 0;
        var value = op switch
        {
            "lui" => left << 12, "add" or "addi" => unchecked(left + right), "sub" => unchecked(left - right),
            "and" or "andi" => left & right, "or" or "ori" => left | right, "xor" or "xori" => left ^ right,
            "slli" => left << (int)right, "srli" => left >> (int)right, "srai" => unchecked((uint)((int)left >> (int)right)),
            "slt" => (int)left < (int)right ? 1u : 0u, "sltu" or "sltiu" => left < right ? 1u : 0u,
            _ => throw new AdspException(s, DSNText.Russian("Execution.AssemblyMachine.Text007"))
        };
        Write(operands[0], value, s, changes);
    }
    private void Msp(CpuInstruction instruction, ref int pc, List<MemoryChange> changes)
    {
        var a = instruction.Args; var s = instruction.Source; var op = instruction.Op; var flags = Flags;
        if (op == "nop") return;
        if (op is "jmp" or "jz" or "jnz" or "jl" or "jge" or "jc" or "jnc")
        {
            var take = op switch { "jmp" => true, "jz" => flags.Zero, "jnz" => !flags.Zero,
                "jl" => flags.Negative ^ flags.Overflow, "jge" => !(flags.Negative ^ flags.Overflow), "jc" => flags.Carry, _ => !flags.Carry };
            if (take) pc = a[0].Value; return;
        }
        var source = (ushort)Read(a[0], s); var dest = (ushort)Read(a[1], s);
        var result = op switch
        {
            "mov.w" => source, "and.w" or "bit.w" => dest & source, "bis.w" => dest | source,
            "bic.w" => dest & ~source, "xor.w" => dest ^ source, "add.w" => dest + source,
            "sub.w" or "cmp.w" => dest - source, _ => throw new AdspException(s, DSNText.Russian("Execution.AssemblyMachine.Text008"))
        };
        var word = (ushort)result;
        if (op is not ("cmp.w" or "bit.w")) Write(a[1], word, s, changes);
        if (op is "mov.w" or "bis.w" or "bic.w") return;
        var overflow = op switch
        {
            "add.w" => ((~(dest ^ source) & (dest ^ word)) & 0x8000) != 0,
            "sub.w" or "cmp.w" => (((dest ^ source) & (dest ^ word)) & 0x8000) != 0,
            "xor.w" => (dest & source & 0x8000) != 0, _ => false
        };
        var carry = op switch { "add.w" => result > 65535, "sub.w" or "cmp.w" => dest >= source, _ => word != 0 };
        registers[2] = (word == 0 ? 2u : 0) | ((word & 0x8000) != 0 ? 4u : 0) | (overflow ? 256u : 0) | (carry ? 1u : 0);
    }
}
