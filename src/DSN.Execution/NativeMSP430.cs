// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DSN.Core;

namespace DSN.Execution;

internal static class NativeMSP430
{
    public static NativeSource Generate(AssemblyProgram program, string? dsnSha256)
    {
        var image = program.InitialImage; var text = new StringBuilder(); var unique = 0;
        var stateRegisters = new[] { 2 }.Concat(Enumerable.Range(4, 12).Where(r => r != 12)).ToArray();
        var words = program.Addresses.Select((p, i) => new NativeWord(p.Key, "dsn_word_" + i, p.Value)).ToArray();
        void Line(string line = "") => text.AppendLine(line);
        Line(".cpu 430"); Line(".mspabi_attribute 4,1"); Line(".mspabi_attribute 6,1"); Line(".mspabi_attribute 8,1");
        foreach (var word in words) { Line(".global " + word.Symbol); Line($".set {word.Symbol}, dsn_memory + {word.WordOffset * 2}"); }
        Line(".section .text.dsn,\"ax\",@progbits"); Line(".balign 2");
        Begin("dsn_init"); SaveCaller();
        Line("    mov.w #dsn_initial_image,r4"); Line("    mov.w #dsn_memory,r5"); Line($"    mov.w #{image.Length},r6");
        Line("    cmp.w #0,r6"); Line("    jz .Ldsn_init_registers"); Line(".Ldsn_init_words:");
        Line("    mov.w @r4+,0(r5)"); Line("    add.w #2,r5"); Line("    sub.w #1,r6"); Line("    jnz .Ldsn_init_words");
        Line(".Ldsn_init_registers:");
        for (var i = 0; i < stateRegisters.Length; i++) Line($"    mov.w #0,&dsn_registers+{i * 2}");
        Line("    mov.w #0,&dsn_status"); RestoreCaller(); End("dsn_init");
        Begin("dsn_step"); SaveCaller();
        Line("    cmp.w #0,&dsn_status"); Line("    jz .Ldsn_step_begin"); Line("    br #.Ldsn_restore_caller"); Line(".Ldsn_step_begin:");
        foreach (var (register, i) in stateRegisters.Select((r, i) => (r, i)).Where(p => p.r != 2)) Line($"    mov.w &dsn_registers+{i * 2},r{register}");
        Line("    mov.w &dsn_registers,r2"); Line("    nop");
        for (var b = 0; b < program.Blocks.Count; b++)
        {
            var block = program.Blocks[b];
            for (var pc = 0; pc < block.Code.Length; pc++)
            {
                var instruction = block.Code[pc]; var operands = instruction.Args; var op = instruction.Op;
                Line($".Ldsn_b{b}_i{pc}:");
                if (op == "nop") { Line("    nop"); continue; }
                if (op is "jmp" or "jz" or "jnz" or "jl" or "jge" or "jc" or "jnc")
                {
                    var target = $".Ldsn_b{b}_i{operands[0].Value}";
                    if (op == "jmp") Line("    br #" + target);
                    else
                    {
                        var inverse = op switch { "jz" => "jnz", "jnz" => "jz", "jl" => "jge", "jge" => "jl", "jc" => "jnc", _ => "jc" };
                        var next = ".Ldsn_jump_" + unique++; Line($"    {inverse} {next}"); Line("    br #" + target); Line(next + ":");
                    }
                    continue;
                }
                string Register(CpuOperand operand)
                {
                    if (operand.Register == 12) throw new AdspException(instruction.Source, DSNText.Russian("Native.Register", "R12"));
                    return "r" + operand.Register;
                }
                string Operand(CpuOperand operand)
                {
                    if (operand.Memory) return operand.Indirect ? "0(r12)" : $"&dsn_memory+{operand.Value}";
                    return operand.Register >= 0 ? Register(operand) : "#" + operand.Value;
                }
                void Address(CpuOperand operand, int discard)
                {
                    var id = unique++; var aligned = ".Ldsn_align_" + id; var valid = ".Ldsn_valid_" + id;
                    Line("    push.w r2"); Line($"    mov.w {Register(operand)},r12");
                    if (operand.Value != 0) Line($"    add.w #{operand.Value},r12");
                    Line("    bit.w #1,r12"); Line("    jz " + aligned); Fault(); Line(aligned + ":");
                    Line($"    cmp.w #{image.Length * 2},r12"); Line("    jlo " + valid); Fault(); Line(valid + ":");
                    Line("    add.w #dsn_memory,r12"); Line("    pop.w r2"); Line("    nop");
                    void Fault()
                    {
                        Line("    pop.w r2"); Line("    nop");
                        for (var d = 0; d < discard; d++) Line("    pop.w r12");
                        Line("    br #.Ldsn_memory_fault");
                    }
                }
                var sourceIndirect = operands[0].Memory && operands[0].Indirect;
                var destinationIndirect = operands[1].Memory && operands[1].Indirect;
                var source = Operand(operands[0]); var destination = Operand(operands[1]);
                if (sourceIndirect)
                {
                    Address(operands[0], 0);
                    if (destinationIndirect) { Line("    mov.w 0(r12),r12"); Line("    push.w r12"); source = "0(r1)"; }
                }
                if (destinationIndirect) Address(operands[1], sourceIndirect ? 1 : 0);
                Line($"    {op} {source},{destination}");
                if (sourceIndirect && destinationIndirect) Line("    pop.w r12");
            }
            Line($".Ldsn_b{b}_i{block.Code.Length}:");
        }
        Line("    br #.Ldsn_save_state"); Line(".Ldsn_memory_fault:"); Line("    mov.w #1,&dsn_status"); Line(".Ldsn_save_state:");
        foreach (var (register, i) in stateRegisters.Select((r, i) => (r, i))) Line($"    mov.w r{register},&dsn_registers+{i * 2}");
        Line(".Ldsn_restore_caller:"); RestoreCaller(); End("dsn_step");
        Line(".section .rodata.dsn,\"a\",@progbits"); Line(".balign 2"); Line("dsn_initial_image:");
        foreach (var batch in image.Chunk(16)) Line("    .word " + string.Join(",", batch));
        Line(".section .bss.dsn,\"aw\",@nobits"); Line(".balign 2"); Line(".global dsn_memory"); Line("dsn_memory:");
        Line($"    .space {Math.Max(2, image.Length * 2)}"); Line("dsn_registers:"); Line($"    .space {stateRegisters.Length * 2}");
        Line(".global dsn_status"); Line("dsn_status:"); Line("    .space 2");
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "DSNCompiler.NativeModule", Version = 1, Generator = NativeAssembly.FormatVersion,
            Architecture = "MSP430", Isa = "msp430", Abi = "msp430-small", Target = "MSP430F1611", DSNSha256 = dsnSha256,
            program.LibrarySha256, program.KernelId, MemoryWords = image.Length, Entries = new[] { "dsn_init", "dsn_step" }, Status = "dsn_status", Words = words
        });
        Line(".section .dsn.meta,\"\",@progbits");
        foreach (var batch in metadata.Chunk(32)) Line("    .byte " + string.Join(",", batch));
        var assembly = text.ToString().Replace("\r\n", "\n");
        return new("MSP430", "msp430", "msp430-small", program.LibrarySha256, assembly, Linker, words,
            image.Length, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assembly))), dsnSha256);

        void Begin(string name) { Line(".global " + name); Line(".type " + name + ",@function"); Line(name + ":"); }
        void End(string name) => Line(".size " + name + ",.-" + name);
        void SaveCaller() { Line("    push.w r2"); for (var r = 4; r <= 15; r++) Line($"    push.w r{r}"); }
        void RestoreCaller() { for (var r = 15; r >= 4; r--) Line($"    pop.w r{r}"); Line("    pop.w r2"); Line("    nop"); Line("    ret"); }
    }

    private const string Linker = """
        OUTPUT_ARCH(msp430)
        ENTRY(_start)
        MEMORY {
          RAM (rw) : ORIGIN = 0x1100, LENGTH = 10K
          FLASH (rx) : ORIGIN = 0x4000, LENGTH = 0xbfe0
          VECTORS (rx) : ORIGIN = 0xffe0, LENGTH = 0x20
        }
        SECTIONS {
          .text : { *(.text.start) *(.text*) } > FLASH
          .rodata : { *(.rodata*) } > FLASH
          .bss (NOLOAD) : { *(.bss*) *(COMMON) } > RAM
          __dsn_end = .;
          __stack_top = ORIGIN(RAM) + LENGTH(RAM);
          .resetvec 0xfffe : { KEEP(*(.resetvec)) } > VECTORS
          ASSERT(__dsn_end + 512 <= __stack_top, "DSN RAM capacity")
        }
        """;
}
