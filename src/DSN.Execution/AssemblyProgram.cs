// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DSN.Execution;

internal sealed record CpuOperand(int Register = -1, int Value = 0, bool Memory = false, bool Indirect = false);
internal sealed record CpuInstruction(string Op, CpuOperand[] Args, SourceLocation Source);
internal sealed record AssemblyBlock(string Instance, string Macro, CpuInstruction[] Code, SourceLocation Source);

public sealed class AssemblyProgram : IExecutionProgram
{
    private readonly Dictionary<string, int> addresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> symbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ushort> initial = [];
    private readonly List<string> names = [];
    private readonly Dictionary<int, ushort> initialized = [];
    internal List<AssemblyBlock> Blocks { get; } = [];
    public string Architecture { get; }
    public string KernelId => Architecture == "RV32I" ? "rv32i-logic-v2" : "msp430-logic-v1";
    public string LibrarySha256 { get; }
    public IReadOnlyDictionary<string, int> Addresses => new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(addresses);
    public ushort[] InitialImage => initial.ToArray();
    public IReadOnlyList<string> RegisterNames => Enumerable.Range(0, Architecture == "RV32I" ? 32 : 16).Select(i => DSNText.Concat((Architecture == "RV32I" ? "X" : "R"), i)).ToArray();
    public IReadOnlyList<string> InstanceOrder => Blocks.Select(b => b.Instance).ToArray();
    public IReadOnlyList<ExecutionBlockInfo> BlockInfo => Blocks.Select(b => new ExecutionBlockInfo(b.Instance, b.Macro)).ToArray();
    public SourceLocation SourceForInstance(string instance) => Blocks.First(b => b.Instance == instance).Source;
    public IExecutionMachine CreateMachine() => new AssemblyMachine(this);
    internal string Name(int address) => names[address];
    private AssemblyProgram(AssemblyLibrary library) { Architecture = library.Architecture; LibrarySha256 = library.Sha256; }

    public static AssemblyProgram Compile(AssemblyLibrary library, IReadOnlyList<MacroInvocation> calls,
        IReadOnlyDictionary<string, ushort> words, IReadOnlyDictionary<string, int>? constants = null,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        if (calls.Count > 4096) throw new ArgumentException(DSNText.Russian("Execution.AdspProgram.Text006"));
        var program = new AssemblyProgram(library);
        foreach (var (name, value) in words)
        {
            if (!AdspLibrary.Identifier(name)) throw new ArgumentException(DSNText.Russian("Execution.AssemblyProgram.Text001"));
            program.Allocate(name, name, value);
        }
        foreach (var (alias, target) in aliases ?? new Dictionary<string, string>())
        {
            if (!AdspLibrary.Identifier(alias) || !program.addresses.TryGetValue(target, out var index) || !program.addresses.TryAdd(alias, index))
                throw new ArgumentException(DSNText.Russian("Execution.AssemblyProgram.Text002"));
            program.symbols.Add(alias, index * 2);
        }
        foreach (var (name, value) in constants ?? new Dictionary<string, int>())
            if (!AdspLibrary.Identifier(name) || !program.symbols.TryAdd(name, value)) throw new ArgumentException(DSNText.Russian("Execution.AssemblyProgram.Text003"));
        var instances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var call in calls)
        {
            if (!AdspLibrary.Identifier(call.Instance) || !instances.Add(call.Instance)) throw new ArgumentException(DSNText.Russian("Execution.AssemblyProgram.Text004"));
            if (!library.Macros.TryGetValue(call.Macro, out var macro)) throw new AdspException(new(DSNText.Russian("Execution.AdspProgram.Text008"), 1, call.Instance), DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text005"), call.Macro));
            program.CompileBlock(macro, call, program.Blocks.Count);
        }
        return program;
    }
    private void Allocate(string name, string symbol, ushort value)
    {
        if (initial.Count >= 16384 || !addresses.TryAdd(name, initial.Count) || !symbols.TryAdd(symbol, initial.Count * 2))
            throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text006"), name));
        initial.Add(value); names.Add(name);
    }
    private void Initialize(int byteAddress, int value, SourceLocation source)
    {
        if (byteAddress < 0 || byteAddress % 2 != 0 || byteAddress / 2 >= initial.Count || value is < -32768 or > 65535)
            throw new AdspException(source, DSNText.Russian("Execution.AssemblyProgram.Text007"));
        var word = unchecked((ushort)value); var index = byteAddress / 2;
        if (initialized.TryGetValue(index, out var previous) && previous != word) throw new AdspException(source, DSNText.Russian("Execution.AssemblyProgram.Text008"));
        initialized[index] = word; initial[index] = word;
    }
    private void CompileBlock(AssemblyMacro macro, MacroInvocation call, int serial)
    {
        if (call.Arguments.Count != macro.Parameters.Length) throw new AdspException(macro.Source, DSNText.Russian("Execution.AssemblyProgram.Text009"));
        var lines = macro.Lines.Select(line =>
        {
            var text = line.Text.Replace("\\@", DSNText.Concat("_", serial));
            for (var i = 0; i < macro.Parameters.Length; i++)
                text = Regex.Replace(text, DSNText.Concat(DSNText.Concat(@"\\", Regex.Escape(macro.Parameters[i])), @"\b"), _ => DSNText.Concat(DSNText.Concat("(", call.Arguments[i]), ")"), RegexOptions.IgnoreCase);
            if (text.Contains('\\')) throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text010"));
            return new AssemblyLine(text, line.Source with { Instance = call.Instance, Macro = call.Macro });
        }).ToArray();
        var codeLines = new List<AssemblyLine>(); var sections = new Stack<string>(); var section = "text";
        string? dataLabel = null; int? initAddress = null;
        foreach (var line in lines)
        {
            var text = line.Text;
            if (text.StartsWith(".pushsection ", StringComparison.OrdinalIgnoreCase))
            {
                var target = text[13..].Split(',')[0].Trim();
                if (target is not (".data" or ".dsn_init") || section != "text") throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text011"));
                sections.Push(section); section = target; continue;
            }
            if (text == ".popsection")
            {
                if (!sections.TryPop(out section!) || dataLabel is not null || initAddress is not null) throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text012"));
                continue;
            }
            if (section == ".data")
            {
                if (Regex.IsMatch(text, @"^\.L\w+:$"))
                {
                    if (dataLabel is not null) throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text013"));
                    dataLabel = text[..^1];
                }
                else if (text.StartsWith(".short ", StringComparison.OrdinalIgnoreCase) && dataLabel is not null)
                {
                    var suffix = DSNText.Concat("_", serial);
                    if (!dataLabel.EndsWith(suffix, StringComparison.Ordinal)) throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text014"));
                    var name = DSNText.Concat(DSNText.Concat(call.Instance, "::"), dataLabel[2..^suffix.Length]);
                    Allocate(name, dataLabel, 0); Initialize(symbols[dataLabel], Expression(text[7..], line.Source), line.Source); dataLabel = null;
                }
                else throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text015"));
                continue;
            }
            if (section == ".dsn_init")
            {
                if (text.StartsWith(".long ", StringComparison.OrdinalIgnoreCase) && initAddress is null) initAddress = Expression(text[6..], line.Source);
                else if (text.StartsWith(".short ", StringComparison.OrdinalIgnoreCase) && initAddress is not null)
                { Initialize(initAddress.Value, Expression(text[7..], line.Source), line.Source); initAddress = null; }
                else throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text016"));
                continue;
            }
            codeLines.Add(line);
        }
        if (sections.Count != 0) throw new AdspException(macro.Source, DSNText.Russian("Execution.AssemblyProgram.Text017"));
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); var code = new List<CpuInstruction>();
        var jumps = new List<(int Index, string Label)>();
        foreach (var line in codeLines)
        {
            if (line.Text.EndsWith(':'))
            {
                if (!Regex.IsMatch(line.Text, @"^\.L\w+:$") || !labels.TryAdd(line.Text[..^1], code.Count)) throw new AdspException(line.Source, DSNText.Russian("Execution.AssemblyProgram.Text018"));
                continue;
            }
            var pieces = line.Text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            var op = pieces[0].ToLowerInvariant(); var args = pieces.Length == 1 ? [] : pieces[1].Split(',').Select(a => a.Trim()).ToArray();
            if (Architecture == "RV32I") CompileRisc(op, args, line.Source, code, jumps);
            else CompileMsp(op, args, line.Source, code, jumps);
        }
        foreach (var (index, label) in jumps)
        {
            if (!labels.TryGetValue(label, out var target)) throw new AdspException(code[index].Source, DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text019"), label));
            var instruction = code[index]; var args = instruction.Args.ToArray(); args[^1] = new(Value: target); code[index] = instruction with { Args = args };
        }
        Blocks.Add(new(call.Instance, call.Macro, code.ToArray(), macro.Source with { Instance = call.Instance, Macro = call.Macro }));
    }
    private void CompileRisc(string op, string[] a, SourceLocation s, List<CpuInstruction> code, List<(int, string)> jumps)
    {
        CpuOperand R(string v) => new(Register: Register(v, s));
        CpuOperand I(string v) => new(Value: Expression(v, s));
        void Emit(string name, params CpuOperand[] operands) => code.Add(new(name, operands, s));
        if (op is "li" or "la" && a.Length == 2)
        {
            var value = Expression(a[1], s); var lower = (value << 20) >> 20;
            if (value != lower) { Emit("lui", R(a[0]), new(Value: (value - lower) >> 12)); Emit("addi", R(a[0]), R(a[0]), new(Value: lower)); }
            else Emit("addi", R(a[0]), new(Register: 0), new(Value: value));
        }
        else if (op is "mv" or "not" && a.Length == 2) Emit(op == "mv" ? "addi" : "xori", R(a[0]), R(a[1]), new(Value: op == "mv" ? 0 : -1));
        else if (op == "lui" && a.Length == 2)
        {
            var upper = Expression(a[1], s); if (upper is < 0 or > 0xfffff) throw new AdspException(s, DSNText.Russian("Execution.AssemblyProgram.Text020"));
            Emit("lui", R(a[0]), new(Value: upper));
        }
        else if (op == "j" && a.Length == 1) { jumps.Add((code.Count, a[0])); Emit("j", new CpuOperand()); }
        else if (op is "beq" or "bne" or "blt" or "bge" or "bltu" or "bgeu" && a.Length == 3)
        { jumps.Add((code.Count, a[2])); Emit(op, R(a[0]), R(a[1]), new()); }
        else if (op is "lhu" or "lh" or "sh" && a.Length == 2)
        {
            var match = Regex.Match(a[1], @"^(.*)\((\w+)\)$");
            if (!match.Success) throw new AdspException(s, DSNText.Russian("Execution.AssemblyProgram.Text021"));
            var offset = Expression(match.Groups[1].Value, s); if (offset is < -2048 or > 2047) throw new AdspException(s, DSNText.Russian("Execution.AssemblyProgram.Text022"));
            Emit(op, R(a[0]), new(Register: Register(match.Groups[2].Value, s), Value: offset, Memory: true, Indirect: true));
        }
        else if (op is "add" or "sub" or "and" or "or" or "xor" or "slt" or "sltu" && a.Length == 3) Emit(op, R(a[0]), R(a[1]), R(a[2]));
        else if (op is "addi" or "andi" or "ori" or "xori" or "sltiu" or "slli" or "srli" or "srai" && a.Length == 3)
        {
            var value = Expression(a[2], s); var shift = op is "slli" or "srli" or "srai";
            if (shift ? value is < 0 or > 31 : value is < -2048 or > 2047) throw new AdspException(s, DSNText.Russian("Execution.AssemblyProgram.Text023"));
            Emit(op, R(a[0]), R(a[1]), I(a[2]));
        }
        else throw new AdspException(s, DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text024"), op));
    }
    private void CompileMsp(string op, string[] a, SourceLocation s, List<CpuInstruction> code, List<(int, string)> jumps)
    {
        if (op is "jmp" or "jz" or "jnz" or "jl" or "jge" or "jc" or "jnc" && a.Length == 1)
        { jumps.Add((code.Count, a[0])); code.Add(new(op, [new()], s)); return; }
        if (op == "nop" && a.Length == 0) { code.Add(new(op, [], s)); return; }
        if (op is not ("mov.w" or "and.w" or "bis.w" or "bic.w" or "xor.w" or "add.w" or "sub.w" or "cmp.w" or "bit.w") || a.Length != 2)
            throw new AdspException(s, DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text025"), op));
        CpuOperand Parse(string text)
        {
            if (text.StartsWith('#')) return new(Value: Expression(text[1..], s));
            if (text.StartsWith('&')) return new(Value: Expression(text[1..], s), Memory: true);
            var match = Regex.Match(text, @"^(.*)\((r\d+)\)$", RegexOptions.IgnoreCase);
            if (match.Success) return new(Register: Register(match.Groups[2].Value, s), Value: Expression(match.Groups[1].Value, s), Memory: true, Indirect: true);
            return new(Register: Register(text, s));
        }
        var source = Parse(a[0]); var dest = Parse(a[1]);
        if (!dest.Memory && dest.Register is not (>= 2 and <= 15)) throw new AdspException(s, DSNText.Russian("Execution.AssemblyProgram.Text026"));
        foreach (var operand in new[] { source, dest })
            if (operand.Value is < -32768 or > 65535 || operand.Memory && !operand.Indirect && (operand.Value < 0 || operand.Value % 2 != 0 || operand.Value / 2 >= initial.Count))
                throw new AdspException(s, DSNText.Russian("Execution.AssemblyProgram.Text027"));
        code.Add(new(op, [source, dest], s));
    }
    private int Register(string name, SourceLocation s)
    {
        name = name.ToLowerInvariant();
        if (Architecture == "MSP430" && name.StartsWith('r') && int.TryParse(name[1..], out var msp) && msp is >= 2 and <= 15 && msp != 3) return msp;
        if (Architecture == "RV32I")
        {
            if (name.StartsWith('x') && int.TryParse(name[1..], out var number) && number is >= 0 and <= 31) return number;
            var aliases = new[] { "zero", "ra", "sp", "gp", "tp", "t0", "t1", "t2", "s0", "s1", "a0", "a1", "a2", "a3", "a4", "a5", "a6", "a7", "s2", "s3", "s4", "s5", "s6", "s7", "s8", "s9", "s10", "s11", "t3", "t4", "t5", "t6" };
            var index = Array.IndexOf(aliases, name); if (index >= 0) return index;
        }
        throw new AdspException(s, DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text028"), name));
    }
    private int Expression(string text, SourceLocation source)
    {
        var tokens = Regex.Matches(text, @"0[xX][0-9a-fA-F]+|[A-Za-z_.][A-Za-z0-9_.]*|\d+|[()+*/-]").Select(m => m.Value).ToArray();
        if (string.Concat(tokens) != Regex.Replace(text, @"\s", "") || tokens.Length == 0 || tokens.Length > 128) throw new AdspException(source, DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text029"), text));
        var p = 0;
        long Atom()
        {
            if (p >= tokens.Length) throw new AdspException(source, DSNText.Russian("Execution.AssemblyProgram.Text030"));
            var token = tokens[p++];
            if (token == "+") return Atom(); if (token == "-") return -Atom();
            if (token == "(") { var v = Sum(); if (p >= tokens.Length || tokens[p++] != ")") throw new AdspException(source, DSNText.Russian("Execution.AssemblyProgram.Text031")); return v; }
            if (symbols.TryGetValue(token, out var value)) return value;
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return unchecked((int)hex);
            if (int.TryParse(token, out value)) return value;
            throw new AdspException(source, DSNText.Concat(DSNText.Russian("Execution.AssemblyProgram.Text032"), token));
        }
        long Product() { var v = Atom(); while (p < tokens.Length && tokens[p] is "*" or "/") { var op = tokens[p++]; var r = Atom(); v = op == "*" ? checked(v * r) : r != 0 ? v / r : throw new AdspException(source, DSNText.Russian("Execution.AssemblyProgram.Text033")); } return v; }
        long Sum() { var v = Product(); while (p < tokens.Length && tokens[p] is "+" or "-") { var op = tokens[p++]; var r = Product(); v = op == "+" ? checked(v + r) : checked(v - r); } return v; }
        try { var result = Sum(); if (p != tokens.Length || result is < int.MinValue or > int.MaxValue) throw new AdspException(source, DSNText.Russian("Execution.AssemblyProgram.Text034")); return (int)result; }
        catch (OverflowException) { throw new AdspException(source, DSNText.Russian("Execution.AdspProgram.Text037")); }
    }
}
