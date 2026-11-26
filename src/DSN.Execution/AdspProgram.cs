// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DSN.Execution;

public sealed record MacroInvocation(string Instance, string Macro, IReadOnlyList<string> Arguments);
public sealed record AdspFlags(bool Zero, bool Negative, bool Overflow, bool Carry);
public sealed record MemoryChange(string Name, ushort Before, ushort After);
public sealed record FunctionExecutionRecord(string Instance, string Macro, int Instructions, IReadOnlyList<MemoryChange> Changes);
public sealed record StepExecutionRecord(long Tick, IReadOnlyList<FunctionExecutionRecord> Blocks, AdspFlags Flags);
internal enum Operation { Move, Pass, Not, And, Or, Xor, Add, Subtract, Negate, Jump, Nop }
internal enum Condition { Always, EQ, NE, LE, LT, GE, GT, AV, AC }
internal enum OperandKind { Register, Memory, Immediate, IndirectMemory }
internal sealed record Operand(OperandKind Kind, int Value, int Modifier = -1);
internal sealed record Instruction(Operation Operation, Condition Condition, Operand? Destination,
    Operand? Left, Operand? Right, int Target, SourceLocation Source);
internal sealed record BlockCode(string Instance, string Macro, Instruction[] Instructions, SourceLocation Source);

public sealed class AdspProgram : IExecutionProgram
{
    public const string KernelVersion = "adsp-logic-v3";
    public string Architecture => "ADSP21";
    public string KernelId => KernelVersion;
    public IReadOnlyList<string> RegisterNames => Registers;
    public IReadOnlyList<ExecutionBlockInfo> BlockInfo => blocks.Select(b => new ExecutionBlockInfo(b.Instance, b.Macro)).ToArray();
    ushort[] IExecutionProgram.InitialImage => InitialImage;
    IExecutionMachine IExecutionProgram.CreateMachine() => CreateMachine();
    internal static readonly string[] Registers = ["AX0", "AX1", "AY0", "AY1", "AR", "AF", "I3", "M3"];
    private readonly Dictionary<string, int> addresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ushort> initial = [];
    private readonly List<string> names = [];
    private readonly List<BlockCode> blocks = [];
    private readonly Dictionary<int, ushort> initialized = [];
    public string LibrarySha256 { get; }
    public IReadOnlyDictionary<string, int> Addresses => new ReadOnlyDictionary<string, int>(addresses);
    public IReadOnlyList<string> InstanceOrder => blocks.Select(x => x.Instance).ToArray();
    public SourceLocation SourceForInstance(string instance) => blocks.First(b => b.Instance.Equals(instance, StringComparison.OrdinalIgnoreCase)).Source;
    internal ushort[] InitialImage => initial.ToArray();
    internal IReadOnlyList<BlockCode> Blocks => blocks;
    internal string Name(int address) => names[address];
    private AdspProgram(string hash) { LibrarySha256 = hash; }

    public static AdspProgram Compile(AdspLibrary library, IReadOnlyList<MacroInvocation> invocations,
        IReadOnlyDictionary<string, ushort> words, IReadOnlyDictionary<string, int>? constants = null,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        var program = new AdspProgram(library.Sha256);
        var values = new Dictionary<string, int>(constants ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in words)
        {
            if (!AdspLibrary.Identifier(name) || Registers.Contains(name, StringComparer.OrdinalIgnoreCase) || values.ContainsKey(name))
                throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text001"), name));
            program.Allocate(name, value, new(DSNText.Russian("Execution.AdspProgram.Text002"), 1));
        }
        if (aliases is not null)
            foreach (var (alias, target) in aliases)
            {
                if (!AdspLibrary.Identifier(alias) || Registers.Contains(alias, StringComparer.OrdinalIgnoreCase) || values.ContainsKey(alias) ||
                    !program.addresses.TryGetValue(target, out var address)) throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text003"), alias));
                if (program.addresses.TryGetValue(alias, out var previous) && previous != address) throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text004"), alias));
                program.addresses[alias] = address;
            }
        foreach (var (name, _) in values)
            if (!AdspLibrary.Identifier(name) || Registers.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text005"), name));
        var instanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (invocations.Count > 4096) throw new ArgumentException(DSNText.Russian("Execution.AdspProgram.Text006"));
        foreach (var invocation in invocations)
        {
            if (!AdspLibrary.Identifier(invocation.Instance) || !instanceIds.Add(invocation.Instance))
                throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text007"), invocation.Instance));
            if (!library.Definitions.TryGetValue(invocation.Macro, out var macro))
                throw new AdspException(new(DSNText.Russian("Execution.AdspProgram.Text008"), 1, invocation.Instance, invocation.Macro), DSNText.Russian("Execution.AdspProgram.Text009"));
            if (invocation.Arguments.Count != macro.Parameters.Length)
                throw new AdspException(macro.Source with { Instance = invocation.Instance, Macro = invocation.Macro }, DSNText.Russian("Execution.AdspProgram.Text010"));
            program.CompileBlock(macro, invocation, values);
        }
        return program;
    }

    private int Allocate(string name, ushort value, SourceLocation source)
    {
        if (initial.Count >= 16384 || !addresses.TryAdd(name, initial.Count)) throw new AdspException(source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text011"), name));
        initial.Add(value); names.Add(name); return initial.Count - 1;
    }

    private void CompileBlock(MacroDefinition macro, MacroInvocation invocation, Dictionary<string, int> constants)
    {
        SourceLocation Locate(SourceLocation source) => source with { Instance = invocation.Instance, Macro = macro.Name };
        var locals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var statement in macro.Body.Where(s => s.Tokens[0].Text == ".LOCAL"))
        {
            var t = statement.Tokens;
            if (t.Count < 2 || t.Count % 2 != 0) throw new AdspException(Locate(statement.Source), DSNText.Russian("Execution.AdspProgram.Text012"));
            for (var i = 1; i < t.Count; i += 2)
            {
                if (!AdspLibrary.Identifier(t[i].Text) || Registers.Contains(t[i].Text) || (i + 1 < t.Count && t[i + 1].Text != ",") ||
                    !locals.TryAdd(t[i].Text, DSNText.Concat(DSNText.Concat(invocation.Instance, "::"), t[i].Text)))
                    throw new AdspException(Locate(statement.Source), DSNText.Russian("Execution.AdspProgram.Text013"));
            }
        }
        var arguments = invocation.Arguments.Select((arg, index) =>
        {
            var list = AdspLibrary.Tokenize(DSNText.Concat(arg, ";"), macro.Source.File);
            if (list.Count != 1 || list[0].Tokens.Any(x => x.Text.StartsWith('.') || x.Text.StartsWith('%') || x.Text == ":"))
                throw new AdspException(Locate(macro.Source), DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text014"), index));
            return list[0].Tokens;
        }).ToArray();
        var expanded = macro.Body.Where(s => s.Tokens[0].Text != ".LOCAL").Select(s => new Statement(
            s.Tokens.SelectMany(t =>
            {
                if (t.Text.StartsWith('%'))
                {
                    if (!int.TryParse(t.Text.AsSpan(1), out var i) || i < 0 || i >= arguments.Length)
                        throw new AdspException(Locate(t.Source), DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text015"), t.Text));
                    return arguments[i].Select(a => a with { Source = Locate(t.Source) });
                }
                return new[] { t with { Text = locals.GetValueOrDefault(t.Text, t.Text), Source = Locate(t.Source) } };
            }).ToArray(), Locate(s.Source))).ToArray();
        foreach (var s in expanded.Where(s => s.Tokens[0].Text == ".VAR"))
        {
            var t = s.Tokens.Select(x => x.Text).ToArray();
            if (t.Length != 10 || DSNText.Join(" ", t.Take(8)) != ".VAR / DM / RAM / SEG =" ||
                !AdspLibrary.Identifier(t[8]) || !locals.ContainsValue(t[9]))
                throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text016"));
            Allocate(t[9], 0, s.Source);
        }
        var code = new List<Instruction>(); var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<(int Index, string Label)>();
        foreach (var s in expanded)
        {
            var t = s.Tokens.Select(x => x.Text).ToArray();
            if (t[0] == ".VAR") continue;
            if (t[0] == ".INIT")
            {
                var colon = Array.IndexOf(t, ":");
                if (colon <= 1 || colon == t.Length - 1) throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text017"));
                var address = Address(t[1..colon], constants, s.Source);
                var value = Word(Constant(t[(colon + 1)..], constants, s.Source), s.Source);
                if (initialized.TryGetValue(address, out var previous) && previous != value)
                    throw new AdspException(s.Source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text018"), names[address]));
                initialized[address] = value; initial[address] = value; continue;
            }
            while (t.Length >= 2 && t[1] == ":")
            {
                if (!locals.ContainsValue(t[0]) || addresses.ContainsKey(t[0]) || !labels.TryAdd(t[0], code.Count))
                    throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text019"));
                t = t[2..];
            }
            if (t.Length == 0) continue;
            var condition = Condition.Always;
            if (t[0] == "IF")
            {
                if (t.Length < 3 || !Enum.TryParse(t[1], out condition) || !Enum.IsDefined(condition) || condition == Condition.Always)
                    throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text020"));
                t = t[2..];
            }
            if (t[0] == "JUMP" && t.Length == 2)
            {
                pending.Add((code.Count, t[1])); code.Add(new(Operation.Jump, condition, null, null, null, -1, s.Source)); continue;
            }
            if (t[0] == "NOP" && t.Length == 1) { code.Add(new(Operation.Nop, condition, null, null, null, -1, s.Source)); continue; }
            var equals = Array.IndexOf(t, "=");
            if (equals < 1 || equals == t.Length - 1 || t.Count(x => x == "=") != 1)
                throw new AdspException(s.Source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text021"), DSNText.Join(' ', t)));
            var destination = Destination(t[..equals], constants, s.Source);
            var rhs = t[(equals + 1)..];
            var operation = Operation.Move; Operand left; Operand? right = null;
            if (rhs[0] == "TSTBIT")
            {
                var of = Array.IndexOf(rhs, "OF");
                if (of <= 1 || of == rhs.Length - 1) throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text022"));
                var bit = Constant(rhs[1..of], constants, s.Source);
                if (bit is < 0 or > 15) throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text023"));
                left = Value(rhs[(of + 1)..], constants, s.Source);
                if (left.Kind != OperandKind.Register || left.Value is not (0 or 1 or 4)) throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text024"));
                operation = Operation.And; right = new(OperandKind.Immediate, 1 << bit);
            }
            else if (rhs[0] is "PASS" or "NOT")
            {
                operation = rhs[0] == "PASS" ? Operation.Pass : Operation.Not; left = Value(rhs[1..], constants, s.Source);
                if (operation == Operation.Pass && left.Kind == OperandKind.Immediate && left.Value is not (0 or 1 or 65535))
                    throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text025"));
            }
            else if (!rhs.Any(x => Registers.Contains(x) || x == "DM")) left = Value(rhs, constants, s.Source);
            else
            {
                var binary = FindOperator(rhs, s.Source);
                if (binary > 0)
                {
                    operation = rhs[binary] switch { "AND" => Operation.And, "OR" => Operation.Or, "XOR" => Operation.Xor, "+" => Operation.Add, "-" => Operation.Subtract, _ => throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text026")) };
                    left = Value(rhs[..binary], constants, s.Source); right = Value(rhs[(binary + 1)..], constants, s.Source);
                }
                else if (rhs[0] == "-" && rhs.Length == 2 && Registers.Contains(rhs[1]))
                { operation = Operation.Negate; left = Value(rhs[1..], constants, s.Source); }
                else left = Value(rhs, constants, s.Source);
            }
            if (operation != Operation.Move && (destination.Kind != OperandKind.Register || destination.Value is not (4 or 5) ||
                left.Kind is OperandKind.Memory or OperandKind.IndirectMemory || right?.Kind is OperandKind.Memory or OperandKind.IndirectMemory ||
                left.Kind == OperandKind.Register && left.Value > 5 || right is { Kind: OperandKind.Register, Value: > 5 }))
                throw new AdspException(s.Source, DSNText.Russian("Execution.AdspProgram.Text027"));
            code.Add(new(operation, condition, destination, left, right, -1, s.Source));
        }
        foreach (var (index, label) in pending)
        {
            if (!labels.TryGetValue(label, out var target)) throw new AdspException(code[index].Source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text028"), label));
            code[index] = code[index] with { Target = target };
        }
        blocks.Add(new(invocation.Instance, macro.Name, code.ToArray(), Locate(macro.Source)));
    }

    private static int FindOperator(string[] t, SourceLocation source)
    {
        var depth = 0; var found = -1;
        for (var i = 0; i < t.Length; i++)
        {
            if (t[i] == "(") depth++; else if (t[i] == ")") depth--;
            else if (depth == 0 && i > 0 && t[i] is "AND" or "OR" or "XOR" or "+" or "-")
            {
                if (t[i - 1] is "AND" or "OR" or "XOR" or "+" or "-") continue;
                if (found >= 0) throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text029"));
                found = i;
            }
        }
        return found;
    }
    private Operand Destination(string[] t, Dictionary<string, int> constants, SourceLocation source)
    {
        if (t.Length == 1 && Array.IndexOf(Registers, t[0]) is var register && register >= 0) return new(OperandKind.Register, register);
        if (t.SequenceEqual(new[] { "DM", "(", "I3", ")" })) return new(OperandKind.IndirectMemory, 6);
        if (t.SequenceEqual(new[] { "DM", "(", "I3", ",", "M3", ")" })) return new(OperandKind.IndirectMemory, 6, 7);
        if (t.Length >= 4 && t[0] == "DM" && t[1] == "(" && t[^1] == ")") return new(OperandKind.Memory, Address(t[2..^1], constants, source));
        throw new AdspException(source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text030"), DSNText.Join(' ', t)));
    }
    private Operand Value(string[] t, Dictionary<string, int> constants, SourceLocation source)
    {
        while (t.Length >= 3 && t[0] == "(" && t[^1] == ")") t = t[1..^1];
        if (t.Length == 1 && Array.IndexOf(Registers, t[0]) is var register && register >= 0) return new(OperandKind.Register, register);
        if (t.SequenceEqual(new[] { "DM", "(", "I3", ")" })) return new(OperandKind.IndirectMemory, 6);
        if (t.SequenceEqual(new[] { "DM", "(", "I3", ",", "M3", ")" })) return new(OperandKind.IndirectMemory, 6, 7);
        if (t.Length >= 4 && t[0] == "DM" && t[1] == "(" && t[^1] == ")") return new(OperandKind.Memory, Address(t[2..^1], constants, source));
        return new(OperandKind.Immediate, Word(Constant(t, constants, source), source));
    }
    private int Address(string[] t, Dictionary<string, int> constants, SourceLocation source)
    {
        var address = Constant(t, constants, source);
        if (address < 0 || address >= initial.Count) throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text031"));
        return address;
    }
    private int Constant(string[] t, Dictionary<string, int> constants, SourceLocation source)
    {
        var p = 0;
        int Atom()
        {
            if (p >= t.Length) throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text032"));
            if (t[p] == "-") { p++; return checked(-Atom()); }
            if (t[p] == "+") { p++; return Atom(); }
            if (t[p] == "(") { p++; var v = Sum(); if (p >= t.Length || t[p++] != ")") throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text033")); return v; }
            var symbol = t[p++];
            if (symbol == "^")
            {
                if (p >= t.Length || !addresses.TryGetValue(t[p++], out var pointer)) throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text034"));
                return pointer;
            }
            if (constants.TryGetValue(symbol, out var constant)) return constant;
            if (addresses.TryGetValue(symbol, out var address)) return address;
            if (symbol.StartsWith("0X") && uint.TryParse(symbol.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) && hex <= int.MaxValue) return (int)hex;
            if (symbol.StartsWith("B#") && symbol.Length > 2 && symbol[2..].All(c => c is '0' or '1') && symbol.Length <= 18) return Convert.ToInt32(symbol[2..], 2);
            if (int.TryParse(symbol, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return value;
            throw new AdspException(source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text035"), symbol));
        }
        int Sum()
        {
            var value = Atom();
            while (p < t.Length && t[p] is "+" or "-") { var op = t[p++]; var right = Atom(); value = op == "+" ? checked(value + right) : checked(value - right); }
            return value;
        }
        try { var value = Sum(); if (p != t.Length) throw new AdspException(source, DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text036"), DSNText.Join(' ', t))); return value; }
        catch (OverflowException) { throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text037")); }
    }
    private static ushort Word(int value, SourceLocation source) => value is >= -32768 and <= 65535 ? unchecked((ushort)value) : throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text038"));
    public AdspMachine CreateMachine() => new(this);
}

public sealed class AdspMachine : IExecutionMachine
{
    private readonly AdspProgram program;
    private ushort[] memory = [];
    private readonly ushort[] registers = new ushort[AdspProgram.Registers.Length];
    public long Tick { get; private set; }
    public bool Faulted { get; private set; }
    public AdspFlags Flags { get; private set; } = new(false, false, false, false);
    public IReadOnlyDictionary<string, uint> Registers => AdspProgram.Registers.ToDictionary(n => n, n => (uint)ReadRegister(n));
    internal AdspMachine(AdspProgram program) { this.program = program; Reset(); }
    public void Reset() { memory = program.InitialImage; Array.Clear(registers); Flags = new(false, false, false, false); Tick = 0; Faulted = false; }
    public ushort ReadWord(string name) => memory[Resolve(name)];
    public void WriteWord(string name, ushort value) => memory[Resolve(name)] = value;
    public ushort ReadRegister(string name) { var i = Array.FindIndex(AdspProgram.Registers, x => x.Equals(name, StringComparison.OrdinalIgnoreCase)); return i >= 0 ? registers[i] : throw new ArgumentException(DSNText.Russian("Execution.AdspProgram.Text039")); }
    public IReadOnlyDictionary<string, ushort> Snapshot() => new ReadOnlyDictionary<string, ushort>(program.Addresses.ToDictionary(x => x.Key, x => memory[x.Value], StringComparer.OrdinalIgnoreCase));
    public AdspMachineState CaptureState()
    {
        if (Faulted) throw new InvalidOperationException(DSNText.Russian("Execution.AdspProgram.Text040"));
        return new() { Tick = Tick, MemoryWords = memory.Length, Memory = (ushort[])memory.Clone(), Flags = Flags,
            Registers = AdspProgram.Registers.Select((name, index) => KeyValuePair.Create(name, (uint)registers[index])).ToDictionary() };
    }
    public void RestoreState(AdspMachineState state)
    {
        if (state is null || state.Architecture is not null || state.Tick < 0 || state.Memory is null || state.Flags is null || state.Registers is null ||
            state.MemoryWords != memory.Length || state.Memory.Length != memory.Length || state.Registers.Count != AdspProgram.Registers.Length)
            throw new InvalidDataException(DSNText.Russian("Execution.AdspProgram.Text041"));
        var restoredRegisters = new ushort[registers.Length]; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in state.Registers)
        {
            var index = Array.FindIndex(AdspProgram.Registers, n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || !seen.Add(name) || value > 65535 || index > 5 && value > 0x3fff) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text042"), name));
            restoredRegisters[index] = (ushort)value;
        }
        var restoredMemory = (ushort[])state.Memory.Clone();
        memory = restoredMemory; restoredRegisters.CopyTo(registers, 0); Flags = state.Flags; Tick = state.Tick; Faulted = false;
    }
    private int Resolve(string name) => program.Addresses.TryGetValue(name, out var address) ? address : throw new ArgumentException(DSNText.Concat(DSNText.Russian("Execution.AdspProgram.Text043"), name));
    private int EffectiveAddress(Operand operand, SourceLocation source)
    {
        if (operand.Modifier >= 0 && registers[operand.Modifier] != 0) throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text044"));
        var address = operand.Kind == OperandKind.IndirectMemory ? registers[operand.Value] : operand.Value;
        if (address < 0 || address >= memory.Length) throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text045"));
        return address;
    }
    private ushort Read(Operand operand, SourceLocation source) => operand.Kind switch
    {
        OperandKind.Register => registers[operand.Value], OperandKind.Memory or OperandKind.IndirectMemory => memory[EffectiveAddress(operand, source)],
        _ => (ushort)operand.Value
    };
    private bool Test(Condition c) => c switch
    {
        Condition.Always => true, Condition.EQ => Flags.Zero, Condition.NE => !Flags.Zero,
        Condition.LE => Flags.Zero || (Flags.Negative ^ Flags.Overflow), Condition.LT => Flags.Negative ^ Flags.Overflow,
        Condition.GE => !(Flags.Negative ^ Flags.Overflow), Condition.GT => !Flags.Zero && !(Flags.Negative ^ Flags.Overflow),
        Condition.AV => Flags.Overflow, Condition.AC => Flags.Carry, _ => false
    };
    public StepExecutionRecord Step(IReadOnlyDictionary<string, ushort>? inputs = null, int instructionBudget = 100_000)
    {
        if (Faulted) throw new InvalidOperationException(DSNText.Russian("Execution.AdspProgram.Text046"));
        if (instructionBudget is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(instructionBudget));
        var inputValues = inputs?.Select(x => (Address: Resolve(x.Key), x.Value)).ToArray() ?? [];
        foreach (var (address, value) in inputValues) memory[address] = value;
        var functionRecords = new List<FunctionExecutionRecord>(); var total = 0;
        try
        {
            foreach (var block in program.Blocks)
            {
                var changes = new List<MemoryChange>(); var executed = 0; var pc = 0;
                while (pc < block.Instructions.Length)
                {
                    var instruction = block.Instructions[pc++];
                    if (++total > instructionBudget) throw new AdspException(instruction.Source, DSNText.Russian("Execution.AdspProgram.Text047"));
                    executed++;
                    if (!Test(instruction.Condition)) continue;
                    if (instruction.Operation == Operation.Jump) { pc = instruction.Target; continue; }
                    if (instruction.Operation == Operation.Nop) continue;
                    var a = Read(instruction.Left!, instruction.Source); var b = instruction.Right is null ? (ushort)0 : Read(instruction.Right, instruction.Source);
                    var result = instruction.Operation switch
                    {
                        Operation.Move or Operation.Pass => a, Operation.Not => ~a, Operation.And => a & b,
                        Operation.Or => a | b, Operation.Xor => a ^ b, Operation.Add => a + b,
                        Operation.Subtract => a - b, Operation.Negate => -a, _ => throw new AdspException(instruction.Source, DSNText.Russian("Execution.AdspProgram.Text048"))
                    };
                    var word = unchecked((ushort)result);
                    if (instruction.Operation != Operation.Move)
                    {
                        var overflow = instruction.Operation switch
                        {
                            Operation.Add => ((~(a ^ b) & (a ^ word)) & 0x8000) != 0,
                            Operation.Subtract => (((a ^ b) & (a ^ word)) & 0x8000) != 0,
                            Operation.Negate => a == 0x8000, _ => false
                        };
                        var carry = instruction.Operation switch { Operation.Add => result > 65535, Operation.Subtract => a >= b, Operation.Negate => a == 0, _ => false };
                        Flags = new(word == 0, (word & 0x8000) != 0, overflow, carry);
                    }
                    var destination = instruction.Destination!;
                    if (destination.Kind == OperandKind.Register)
                    {
                        if (destination.Value > 5 && word > 0x3fff) throw new AdspException(instruction.Source, DSNText.Russian("Execution.AdspProgram.Text049"));
                        registers[destination.Value] = word;
                    }
                    else { var address = EffectiveAddress(destination, instruction.Source); changes.Add(new(program.Name(address), memory[address], word)); memory[address] = word; }
                }
                functionRecords.Add(new(block.Instance, block.Macro, executed, changes));
            }
            return new(++Tick, functionRecords, Flags);
        }
        catch { Faulted = true; throw; }
    }
}
