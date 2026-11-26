// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DSN.Core;

namespace DSN.Execution;

internal static class NativeADSP21
{
    public static NativeSource Generate(AdspProgram program, string? dsnSha256)
    {
        var image = program.InitialImage; var text = new StringBuilder();
        var words = program.Addresses.Select(p => new NativeWord(p.Key, Cell(p.Value), p.Value)).ToArray();
        void Line(string line = "") => text.AppendLine(line);
        Line(".SECTION/DM dsn_data;");
        for (var i = 0; i < Math.Max(1, image.Length); i++)
        { Line(".GLOBAL " + Cell(i) + ";"); Line($".VAR {Cell(i)} = {(i < image.Length ? image[i] : 0)};"); }
        Line(".SECTION/DM dsn_runtime;");
        Line(".VAR dsn_registers[9] = 0,0,0,0,0,0,0,0,0;");
        Line(".VAR dsn_caller[10] = 0,0,0,0,0,0,0,0,0,0;");
        Line(".VAR dsn_scratch[7] = 0,0,0,0,0,0,0;");
        Line(".GLOBAL dsn_status;"); Line(".VAR dsn_status = 0;");
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "DSNCompiler.NativeModule", Version = 1, Generator = NativeAssembly.FormatVersion,
            Architecture = "ADSP21", Target = "ADSP-2181", DSNSha256 = dsnSha256, program.LibrarySha256,
            program.KernelId, MemoryWords = image.Length, Entries = new[] { "dsn_init", "dsn_step" }, DataWordBits = 16, InstructionBits = 24
        });
        Line(".VAR dsn_metadata[" + metadata.Length + "] = " + string.Join(",", metadata) + ";");
        Line(".SECTION/PM dsn_code;"); Begin("dsn_init"); SaveRegisters("dsn_caller");
        for (var i = 0; i < image.Length; i++) { Line($"AX0 = {image[i]};"); Line($"DM(dsn_memory+{i}) = AX0;"); }
        Line("AX0 = 0;"); for (var i = 0; i < 9; i++) Line($"DM(dsn_registers+{i}) = AX0;");
        Line("DM(dsn_status) = AX0;"); RestoreRegisters("dsn_caller"); Line("RTS;");
        Begin("dsn_step"); SaveRegisters("dsn_caller");
        Line("AX0 = DM(dsn_status);"); Line("AR = PASS AX0;"); Line("IF NE JUMP dsn_restore_caller;");
        RestoreRegisters("dsn_registers");
        for (var b = 0; b < program.Blocks.Count; b++)
        {
            var block = program.Blocks[b];
            for (var pc = 0; pc < block.Instructions.Length; pc++)
            {
                var instruction = block.Instructions[pc]; Line($"dsn_b{b}_i{pc}:");
                var condition = instruction.Condition;
                if (instruction.Operation == Operation.Jump)
                { Line((condition == Condition.Always ? "" : "IF " + condition + " ") + $"JUMP dsn_b{b}_i{instruction.Target};"); continue; }
                var skip = $"dsn_b{b}_after{pc}";
                if (condition != Condition.Always)
                {
                    var inverse = condition switch { Condition.EQ => "NE", Condition.NE => "EQ", Condition.LE => "GT", Condition.LT => "GE", Condition.GE => "LT", Condition.GT => "LE", Condition.AV => "NOT AV", _ => "NOT AC" };
                    Line($"IF {inverse} JUMP {skip};");
                }
                if (new[] { instruction.Destination, instruction.Left, instruction.Right }.Any(o => o?.Kind == OperandKind.IndirectMemory))
                {
                    SaveScratch();
                    Line("AX0 = I3;"); Line($"AY0 = {image.Length};"); Line("AR = AX0 - AY0;"); Line("IF GE JUMP dsn_memory_fault;");
                    RestoreScratch();
                }
                string Operand(Operand? operand) => operand?.Kind switch
                {
                    OperandKind.Register => AdspProgram.Registers[operand.Value],
                    OperandKind.Immediate => operand.Value == 65535 ? "-1" : operand.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    OperandKind.Memory => $"DM(dsn_memory+{operand.Value})",
                    OperandKind.IndirectMemory => "DM(I3,M3)",
                    _ => ""
                };
                var left = Operand(instruction.Left); var right = Operand(instruction.Right); var destination = Operand(instruction.Destination);
                // AF доступен через АЛУ; перенос данных сохраняет исходные флаги.
                if (instruction.Operation == Operation.Move &&
                    (instruction.Left is { Kind: OperandKind.Register, Value: 5 } || instruction.Destination is { Kind: OperandKind.Register, Value: 5 }))
                {
                    SaveScratch();
                    Line(instruction.Left is { Kind: OperandKind.Register, Value: 5 } ? "AR = PASS AF;" : "AR = " + left + ";");
                    Line("DM(dsn_scratch+4) = AR;");
                    if (instruction.Destination is { Kind: OperandKind.Register, Value: 5 }) Line("AF = PASS AR;");
                    else if (instruction.Destination?.Kind != OperandKind.Register) Line(destination + " = AR;");
                    RestoreScratch();
                    if (instruction.Destination is { Kind: OperandKind.Register } target && target.Value != 5)
                        Line(destination + " = DM(dsn_scratch+4);");
                    if (condition != Condition.Always) Line(skip + ":");
                    continue;
                }
                var expression = instruction.Operation switch
                {
                    Operation.Move => left, Operation.Pass => "PASS " + left, Operation.Not => "NOT " + left,
                    Operation.Negate => "-" + left, Operation.And => left + " AND " + right,
                    Operation.Or => left + " OR " + right, Operation.Xor => left + " XOR " + right,
                    Operation.Add => left + " + " + right, Operation.Subtract => left + " - " + right, _ => ""
                };
                // Общий IR допускает сочетания операндов, для которых АЛУ цели использует входы X/Y.
                if (instruction.Operation is Operation.And or Operation.Or or Operation.Xor or Operation.Add or Operation.Subtract &&
                    !(instruction.Left is { Kind: OperandKind.Register, Value: 0 or 1 or 4 } &&
                    (instruction.Right is { Kind: OperandKind.Register, Value: 2 or 3 or 5 } ||
                     instruction.Right is { Kind: OperandKind.Immediate } immediate && IsAluConstant(instruction.Operation, immediate.Value))))
                {
                    SaveScratch(); SnapshotOperand(instruction.Left!, 4); SnapshotOperand(instruction.Right!, 6);
                    Line("AX0 = DM(dsn_scratch+4);"); Line("AY0 = DM(dsn_scratch+6);");
                    Line("AX1 = DM(dsn_scratch+3);"); Line("ASTAT = AX1;");
                    var op = instruction.Operation switch { Operation.And => "AND", Operation.Or => "OR", Operation.Xor => "XOR", Operation.Add => "+", _ => "-" };
                    Line(destination + " = AX0 " + op + " AY0;");
                    Line("AX0 = DM(dsn_scratch);"); Line("AX1 = DM(dsn_scratch+1);"); Line("AY0 = DM(dsn_scratch+5);");
                    if (instruction.Destination!.Value != 4) Line("AR = DM(dsn_scratch+2);");
                    if (condition != Condition.Always) Line(skip + ":");
                    continue;
                }
                if (instruction.Operation == Operation.Nop) Line("NOP;");
                else Line(destination + " = " + expression + ";");
                if (condition != Condition.Always) Line(skip + ":");
            }
            Line($"dsn_b{b}_i{block.Instructions.Length}:");
        }
        Line("JUMP dsn_save_state;"); Line("dsn_memory_fault:"); RestoreScratch();
        Line("DM(dsn_scratch) = AX0;"); Line("AX0 = 1;"); Line("DM(dsn_status) = AX0;"); Line("AX0 = DM(dsn_scratch);");
        Line("dsn_save_state:"); SaveRegisters("dsn_registers");
        Line("dsn_restore_caller:"); RestoreRegisters("dsn_caller"); Line("RTS;");
        var assembly = text.ToString().Replace("\r\n", "\n");
        return new("ADSP21", "adsp-2181", "dsn-adsp2181-v1", program.LibrarySha256, assembly, Linker, words,
            image.Length, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assembly))), dsnSha256);

        static string Cell(int index) => index == 0 ? "dsn_memory" : "dsn_cell_" + index;
        void Begin(string name) { Line(".GLOBAL " + name + ";"); Line(name + ":"); }
        void SaveRegisters(string target)
        {
            for (var r = 0; r < 5; r++) Line($"DM({target}+{r}) = {AdspProgram.Registers[r]};");
            Line("AX0 = ASTAT;"); Line($"DM({target}+8) = AX0;");
            Line("AR = PASS AF;"); Line($"DM({target}+5) = AR;");
            Line("AX0 = I3;"); Line($"DM({target}+6) = AX0;"); Line("AX0 = M3;"); Line($"DM({target}+7) = AX0;");
            if (target == "dsn_caller") { Line("AX0 = L3;"); Line("DM(dsn_caller+9) = AX0;"); Line("L3 = 0;"); }
        }
        void RestoreRegisters(string target)
        {
            if (target == "dsn_caller") { Line("AX0 = DM(dsn_caller+9);"); Line("L3 = AX0;"); }
            Line($"AX0 = DM({target}+5);"); Line("AF = PASS AX0;");
            Line($"AX0 = DM({target}+6);"); Line("I3 = AX0;"); Line($"AX0 = DM({target}+7);"); Line("M3 = AX0;");
            for (var r = 1; r < 5; r++) Line($"{AdspProgram.Registers[r]} = DM({target}+{r});");
            Line($"AX0 = DM({target}+8);"); Line("ASTAT = AX0;");
            Line($"AX0 = DM({target});");
        }
        void SaveScratch()
        {
            Line("DM(dsn_scratch) = AX0;"); Line("DM(dsn_scratch+1) = AX1;"); Line("DM(dsn_scratch+2) = AR;");
            Line("DM(dsn_scratch+5) = AY0;");
            Line("AX0 = ASTAT;"); Line("DM(dsn_scratch+3) = AX0;");
        }
        void SnapshotOperand(Operand operand, int slot)
        {
            var source = operand.Kind == OperandKind.Register ? operand.Value switch
            {
                0 => "DM(dsn_scratch)", 1 => "DM(dsn_scratch+1)", 2 => "DM(dsn_scratch+5)",
                4 => "DM(dsn_scratch+2)", _ => AdspProgram.Registers[operand.Value]
            } : operand.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Line(operand is { Kind: OperandKind.Register, Value: 5 } ? "AR = PASS AF;" : "AR = " + source + ";");
            Line($"DM(dsn_scratch+{slot}) = AR;");
        }
        void RestoreScratch()
        {
            Line("AX0 = DM(dsn_scratch+3);"); Line("ASTAT = AX0;"); Line("AX1 = DM(dsn_scratch+1);");
            Line("AY0 = DM(dsn_scratch+5);");
            Line("AR = DM(dsn_scratch+2);"); Line("AX0 = DM(dsn_scratch);");
        }
    }

    private static bool IsAluConstant(Operation operation, int value) =>
        operation is Operation.And or Operation.Or or Operation.Xor ? value > 0 && (value & (value - 1)) == 0 : value == 1;

    private const string Linker = """
        ARCHITECTURE(ADSP-2181)
        MEMORY {
          pm_reset { TYPE(PM RAM) START(0x0000) END(0x002f) WIDTH(24) }
          pm_code { TYPE(PM RAM) START(0x0030) END(0x3fff) WIDTH(24) }
          dm_data { TYPE(DM RAM) START(0x0000) END(0x3fdf) WIDTH(16) }
        }
        PROCESSOR p0 {
          OUTPUT($COMMAND_LINE_OUTPUT_FILE)
          SECTIONS {
            dsn_boot { INPUT_SECTIONS($COMMAND_LINE_OBJECTS(dsn_boot)) } > pm_reset
            dsn_code { INPUT_SECTIONS($COMMAND_LINE_OBJECTS(dsn_code)) } > pm_code
            dsn_data { INPUT_SECTIONS($COMMAND_LINE_OBJECTS(dsn_data)) } > dm_data
            dsn_runtime { INPUT_SECTIONS($COMMAND_LINE_OBJECTS(dsn_runtime)) } > dm_data
          }
        }
        """;
}
