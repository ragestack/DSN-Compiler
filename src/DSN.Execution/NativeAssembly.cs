// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DSN.Core;

namespace DSN.Execution;

public sealed record NativeWord(string Name, string Symbol, int WordOffset);
public sealed record NativeSource(string Architecture, string Isa, string Abi, string LibrarySha256,
    string Assembly, string LinkerScript, NativeWord[] Words, int MemoryWords, string SourceSha256, string? DSNSha256 = null);

public static class NativeAssembly
{
    public const string FormatVersion = "dsn-native-v1";

    public static NativeSource Generate(IExecutionProgram program, string? dsnSha256 = null)
    {
        if (dsnSha256 is not null && (dsnSha256.Length != 64 || !dsnSha256.All(Uri.IsHexDigit)))
            throw new ArgumentException(DSNText.Russian("Native.Identity"));
        return program switch
        {
        AssemblyProgram assembly => Generate(assembly, dsnSha256),
        AdspProgram adsp => NativeADSP21.Generate(adsp, dsnSha256),
        _ => throw new ArgumentException(DSNText.Russian("Native.Target", "ADSP21 / RV32I / MSP430"))
        };
    }

    public static NativeSource Generate(AssemblyProgram program, string? dsnSha256 = null)
    {
        if (dsnSha256 is not null && (dsnSha256.Length != 64 || !dsnSha256.All(Uri.IsHexDigit)))
            throw new ArgumentException(DSNText.Russian("Native.Identity"));
        if (program.Architecture == "MSP430") return NativeMSP430.Generate(program, dsnSha256);
        if (program.Architecture != "RV32I") throw new ArgumentException(DSNText.Russian("Native.Target", "RV32I / MSP430"));
        var image = program.InitialImage;
        var text = new StringBuilder();
        void Line(string line = "") => text.AppendLine(line);
        Line(".option norvc"); Line(".option norelax"); Line(".option nopic");
        Line(".attribute arch, \"rv32i2p1\""); Line(".attribute stack_align, 16");
        var words = program.Addresses.Select((p, i) => new NativeWord(p.Key, "dsn_word_" + i, p.Value)).ToArray();
        foreach (var word in words)
        {
            Line(".global " + word.Symbol);
            Line($".set {word.Symbol}, dsn_memory + {word.WordOffset * 2}");
        }
        Line(".section .text.dsn,\"ax\",@progbits"); Line(".balign 4");
        Begin("dsn_init"); SaveCaller();
        Line("    la t0,dsn_initial_image"); Line("    la t1,dsn_memory");
        Line($"    li t2,{image.Length * 2}");
        Line("    beq t2,zero,.Ldsn_init_registers"); Line(".Ldsn_init_words:");
        Line("    lhu t3,0(t0)"); Line("    sh t3,0(t1)");
        Line("    addi t0,t0,2"); Line("    addi t1,t1,2"); Line("    addi t2,t2,-2");
        Line("    bne t2,zero,.Ldsn_init_words"); Line(".Ldsn_init_registers:");
        Line("    la t6,dsn_registers");
        for (var r = 5; r <= 31; r++) Line($"    sw zero,{(r - 5) * 4}(t6)");
        Line("    la t6,dsn_status"); Line("    sw zero,0(t6)");
        RestoreCaller(); End("dsn_init");
        Begin("dsn_step"); SaveCaller(); Line("    la t6,dsn_registers");
        for (var r = 5; r <= 31; r++) Line($"    lw x{r},{(r - 5) * 4}(t6)");
        Line("    la gp,dsn_memory");
        Line("    la tp,dsn_status"); Line("    lw ra,0(tp)"); Line("    beq ra,zero,.Ldsn_step_begin");
        Line("    la tp,.Ldsn_step_done"); Line("    jr tp"); Line(".Ldsn_step_begin:");
        for (var b = 0; b < program.Blocks.Count; b++)
        {
            var block = program.Blocks[b];
            for (var pc = 0; pc < block.Code.Length; pc++)
            {
                var instruction = block.Code[pc]; var op = instruction.Op; var operands = instruction.Args;
                string Register(CpuOperand value)
                {
                    if (value.Register is >= 1 and <= 4)
                        throw new AdspException(instruction.Source, DSNText.Russian("Native.Register", "X" + value.Register));
                    return "x" + value.Register;
                }
                Line($".Ldsn_b{b}_i{pc}:");
                if (op == "j") Line($"    j .Ldsn_b{b}_i{operands[0].Value}");
                else if (op is "beq" or "bne" or "blt" or "bge" or "bltu" or "bgeu")
                    Line($"    {op} {Register(operands[0])},{Register(operands[1])},.Ldsn_b{b}_i{operands[2].Value}");
                else if (op is "lhu" or "lh" or "sh")
                {
                    // Адреса внутри алгоритма остаются смещениями общей DM, включая арифметику указателей.
                    // База размещения прибавляется перед обращением к памяти; алгоритм функции берётся из общего IR.
                    Line($"    addi tp,{Register(operands[1])},{operands[1].Value}");
                    Line("    andi ra,tp,1"); Line($"    bne ra,zero,.Ldsn_b{b}_fault");
                    Line($"    li ra,{image.Length * 2}"); Line($"    bgeu tp,ra,.Ldsn_b{b}_fault");
                    Line("    add tp,gp,tp"); Line($"    {op} {Register(operands[0])},0(tp)");
                }
                else if (op == "lui") Line($"    lui {Register(operands[0])},{operands[1].Value}");
                else
                {
                    var last = op is "addi" or "andi" or "ori" or "xori" or "sltiu" or "slli" or "srli" or "srai"
                        ? operands[2].Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : Register(operands[2]);
                    Line($"    {op} {Register(operands[0])},{Register(operands[1])},{last}");
                }
            }
            Line($".Ldsn_b{b}_i{block.Code.Length}:");
            Line($"    j .Ldsn_b{b}_next"); Line($".Ldsn_b{b}_fault:");
            Line("    la tp,.Ldsn_memory_fault"); Line("    jr tp"); Line($".Ldsn_b{b}_next:");
        }
        Line("    j .Ldsn_step_done"); Line(".Ldsn_memory_fault:");
        Line("    la tp,dsn_status"); Line("    li ra,1"); Line("    sw ra,0(tp)");
        Line(".Ldsn_step_done:");
        Line("    sw t6,120(sp)"); Line("    la t6,dsn_registers");
        for (var r = 5; r < 31; r++) Line($"    sw x{r},{(r - 5) * 4}(t6)");
        Line("    lw t0,120(sp)"); Line("    sw t0,104(t6)");
        RestoreCaller(); End("dsn_step");
        Line(".section .rodata.dsn,\"a\",@progbits"); Line(".balign 2"); Line("dsn_initial_image:");
        foreach (var batch in image.Chunk(16)) Line("    .short " + string.Join(",", batch));
        Line(".section .bss.dsn,\"aw\",@nobits"); Line(".balign 4"); Line(".global dsn_memory"); Line("dsn_memory:");
        Line($"    .space {Math.Max(2, image.Length * 2)}");
        Line(".balign 4"); Line("dsn_registers:"); Line("    .space 108");
        Line(".global dsn_status"); Line("dsn_status:"); Line("    .space 4");
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "DSNCompiler.NativeModule", Version = 1, Generator = FormatVersion,
            Architecture = "RV32I", Isa = "rv32i2p1", Abi = "ilp32", DSNSha256 = dsnSha256,
            program.LibrarySha256, program.KernelId, MemoryWords = image.Length,
            Entries = new[] { "dsn_init", "dsn_step" }, Status = "dsn_status", Words = words
        });
        Line(".section .dsn.meta,\"\",@progbits");
        foreach (var batch in metadata.Chunk(32)) Line("    .byte " + string.Join(",", batch));
        Line(".section .note.GNU-stack,\"\",@progbits");
        var assembly = text.ToString().Replace("\r\n", "\n");
        return new("RV32I", "rv32i2p1", "ilp32", program.LibrarySha256, assembly, RiscLinker,
            words, image.Length, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assembly))), dsnSha256);

        void Begin(string name) { Line(".global " + name); Line(".type " + name + ",@function"); Line(name + ":"); }
        void End(string name) => Line(".size " + name + ",.-" + name);
        void SaveCaller()
        {
            Line("    addi sp,sp,-128"); var slot = 0;
            foreach (var r in Enumerable.Range(1, 31).Where(r => r != 2)) Line($"    sw x{r},{slot++ * 4}(sp)");
        }
        void RestoreCaller()
        {
            var slot = 0;
            foreach (var r in Enumerable.Range(1, 31).Where(r => r != 2)) Line($"    lw x{r},{slot++ * 4}(sp)");
            Line("    addi sp,sp,128"); Line("    ret");
        }
    }

    public static void Write(NativeSource source, string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, source.Architecture == "ADSP21" ? "program.asm" : "program.S"), source.Assembly, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, source.Architecture == "ADSP21" ? "program.ldf" : "program.ld"), source.LinkerScript, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "source.json"), JsonSerializer.Serialize(new
        {
            Format = "DSNCompiler.NativeSource", Version = 1, Generator = FormatVersion,
            source.Architecture, source.Isa, source.Abi, source.DSNSha256, source.LibrarySha256, source.SourceSha256,
            source.MemoryWords, source.Words, Entries = new[] { "dsn_init", "dsn_step" }
        }, DSNProgramBinder.JsonOptions));
    }

    private const string RiscLinker = """
        OUTPUT_ARCH(riscv)
        ENTRY(_start)
        MEMORY { RAM (rwx) : ORIGIN = 0x80000000, LENGTH = 16M }
        SECTIONS {
          .text : { *(.text.start) *(.text*) } > RAM
          .rodata : { *(.rodata*) } > RAM
          .data : { *(.data*) } > RAM
          .bss (NOLOAD) : { *(.bss*) *(COMMON) } > RAM
          . = ALIGN(16);
          __dsn_end = .;
          __stack_top = ORIGIN(RAM) + LENGTH(RAM);
          ASSERT(__dsn_end + 4096 <= __stack_top, "DSN RAM capacity")
        }
        """;
}
