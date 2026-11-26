// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DSN.Core;

namespace DSN.Execution;

public sealed record NativeToolPaths(string Assembler, string Linker, string Inspector, string Disassembler)
{
    public static NativeToolPaths RiscV(string bin)
    {
        bin = Path.GetFullPath(bin);
        return new(Path.Combine(bin, "riscv-none-elf-as.exe"), Path.Combine(bin, "riscv-none-elf-ld.exe"),
            Path.Combine(bin, "riscv-none-elf-readelf.exe"), Path.Combine(bin, "riscv-none-elf-objdump.exe"));
    }
    public static NativeToolPaths Msp430(string bin)
    {
        bin = Path.GetFullPath(bin);
        return new(Path.Combine(bin, "msp430-elf-as.exe"), Path.Combine(bin, "msp430-elf-ld.exe"),
            Path.Combine(bin, "msp430-elf-readelf.exe"), Path.Combine(bin, "msp430-elf-objdump.exe"));
    }
    public static NativeToolPaths ForArchitecture(string architecture, string bin)
    {
        bin = Path.GetFullPath(bin);
        return architecture switch
        {
            "RV32I" => RiscV(bin), "MSP430" => Msp430(bin),
            "ADSP21" => new(Path.Combine(bin, "easm218x.exe"), Path.Combine(bin, "linker.exe"), Path.Combine(bin, "elfdump.exe"), Path.Combine(bin, "elfdump.exe")),
            _ => throw new ArgumentException(DSNText.Russian("Native.Target", "ADSP21 / RV32I / MSP430"))
        };
    }
}

public sealed record NativeArtifact(string File, long Bytes, string Sha256);
public sealed record NativeBuildResult(string Architecture, string Isa, string Abi, string? Image, string Object,
    string BuildRecord, NativeArtifact[] Artifacts);

public static class NativeBuild
{
    public static async Task<NativeBuildResult> BuildAsync(NativeSource source, NativeToolPaths tools, string directory,
        bool linkImage = true, CancellationToken cancellationToken = default)
    {
        if (source.Architecture is not ("ADSP21" or "RV32I" or "MSP430")) throw new ArgumentException(DSNText.Russian("Native.Target", "ADSP21 / RV32I / MSP430"));
        var risc = source.Architecture == "RV32I"; var adsp = source.Architecture == "ADSP21";
        var sourceExtension = adsp ? ".asm" : ".S"; var objectExtension = adsp ? ".doj" : ".o";
        var imageName = adsp ? "program.dxe" : "program.elf"; var objectName = "program" + objectExtension;
        directory = Path.GetFullPath(directory);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new ArgumentException(DSNText.Russian("Native.EmptyFolder"));
        foreach (var path in new[] { tools.Assembler, tools.Inspector, tools.Disassembler }.Concat(linkImage ? [tools.Linker] : []))
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) throw new ArgumentException(DSNText.Russian("Native.Tool", path));
        NativeAssembly.Write(source, directory);
        if (linkImage) File.WriteAllText(Path.Combine(directory, "startup" + sourceExtension), adsp ? AdspStartup : risc ? RiscStartup : MspStartup, new UTF8Encoding(false));
        var versions = new Dictionary<string, string>(); var sequence = 0;
        foreach (var (name, path) in new[] { ("assembler", tools.Assembler), ("linker", tools.Linker), ("inspector", tools.Inspector), ("disassembler", tools.Disassembler) })
        {
            if (!linkImage && name == "linker") continue;
            versions.Add(name, adsp ? "file " + FileVersionInfo.GetVersionInfo(path).FileVersion :
                (await Run(path, ["--version"])).Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim());
        }
        foreach (var name in linkImage ? new[] { "program", "startup" } : new[] { "program" })
            await Run(tools.Assembler, adsp
                ? ["-sp", "-noip", "-proc", "ADSP-2181", "-v", "-l", name + ".lst", "-o", name + objectExtension, name + sourceExtension]
                : (risc ? new[] { "-march=rv32i", "-mabi=ilp32", "-mno-relax" } : new[] { "-mcpu=430", "-mmcu=msp430f1611" })
                    .Concat(["-o", name + objectExtension, name + sourceExtension]).ToArray());
        if (linkImage) await Run(tools.Linker, adsp
            ? ["-proc", "ADSP-2181", "-T", "program.ldf", "-Map", "program.map.xml", "-o", imageName, "startup.doj", objectName]
            : ["-m", risc ? "elf32lriscv" : "msp430elf", "--no-relax", "-T", "program.ld", "-Map", "program.map", "-o", imageName, "startup.o", objectName]);
        var inspectedName = linkImage ? imageName : objectName;
        var inspection = await Run(tools.Inspector, adsp ? ["-all", inspectedName] : ["-h", "-A", "-S", inspectedName]);
        File.WriteAllText(Path.Combine(directory, "elf-inspection.txt"), inspection);
        File.WriteAllText(Path.Combine(directory, "disassembly.txt"), await Run(tools.Disassembler, adsp ? ["-ns", "*", inspectedName] : risc ? ["-d", "-M", "no-aliases", inspectedName] : ["-d", inspectedName]));
        var elf = File.ReadAllBytes(Path.Combine(directory, inspectedName));
        var attributes = adsp ? inspection.Contains("ADSP-218x", StringComparison.Ordinal) && inspection.Contains("dsn_code", StringComparison.Ordinal) : risc
            ? Regex.IsMatch(inspection, "(?m)^\\s*Tag_RISCV_arch:\\s*\"rv32i2p1\"\\s*$")
            : Regex.IsMatch(inspection, @"(?m)^\s*Tag_ISA:\s*MSP430\s*$") &&
                Regex.IsMatch(inspection, @"(?m)^\s*Tag_Code_Model:\s*Small\s*$") && Regex.IsMatch(inspection, @"(?m)^\s*Tag_Data_Model:\s*Small\s*$");
        // Проверяются стандартный заголовок, идентификатор ЦП, ABI и атрибуты, созданные внешним ассемблером.
        if (elf.Length < 52 || !elf.AsSpan(0, 7).SequenceEqual(new byte[] { 127, 69, 76, 70, 1, 1, 1 }) ||
            !adsp && BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(16)) != (linkImage ? 2 : 1) ||
            adsp && BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(16)) != (linkImage ? 8 : 7) ||
            BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(18)) != (adsp ? 30 : risc ? 243 : 105) ||
            !attributes || risc && BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(36)) != 0 ||
            !adsp && !inspection.Contains(".text", StringComparison.Ordinal)) throw new InvalidDataException(DSNText.Russian("Native.Elf", source.Architecture, "elf-inspection.txt"));
        var artifacts = Directory.GetFiles(directory).Order(StringComparer.Ordinal).Select(path => new NativeArtifact(
            Path.GetFileName(path), new FileInfo(path).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
        var buildPath = Path.Combine(directory, "native-build.json");
        File.WriteAllText(buildPath, JsonSerializer.Serialize(new
        {
            Format = "DSNCompiler.NativeBuild", Version = 1, Generator = NativeAssembly.FormatVersion,
            source.Architecture, source.Isa, source.Abi, source.DSNSha256, source.LibrarySha256,
            source.SourceSha256, Entries = new[] { "dsn_init", "dsn_step" },
            MemoryBaseSymbol = "dsn_memory", WordBytes = 2, source.MemoryWords, source.Words,
            Kind = linkImage ? "ExecutableImage" : "RelocatableObject", Image = linkImage ? imageName : null, Object = objectName,
            ToolVersions = versions, Artifacts = artifacts
        }, DSNProgramBinder.JsonOptions));
        return new(source.Architecture, source.Isa, source.Abi, linkImage ? Path.Combine(directory, imageName) : null,
            Path.Combine(directory, objectName), buildPath, artifacts);

        async Task<string> Run(string executable, string[] arguments)
        {
            var id = (++sequence).ToString("00");
            var log = Path.Combine(directory, "tool-" + id + ".log");
            var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            using var process = Process.Start(start) ?? throw new InvalidOperationException(DSNText.Russian("Native.Tool", executable));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token); var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { if (!process.HasExited) process.Kill(true); throw; }
            var output = await stdout; var errors = await stderr;
            File.WriteAllText(log, JsonSerializer.Serialize(new { Tool = Path.GetFileName(executable), Arguments = arguments, process.ExitCode, Output = output, Errors = errors }, DSNProgramBinder.JsonOptions));
            if (process.ExitCode != 0) throw new InvalidOperationException(adsp && ((output + errors).Contains("li1143", StringComparison.Ordinal) || (output + errors).Contains("ea1156", StringComparison.Ordinal))
                ? DSNText.Russian("Native.AdiLicense", directory, log)
                : DSNText.Russian("Native.ToolResult", Path.GetFileName(executable), process.ExitCode, log));
            return output;
        }
    }

    private const string RiscStartup = """
        .option norvc
        .option norelax
        .section .text.start,"ax",@progbits
        .global _start
        _start:
            la sp,__stack_top
            call dsn_init
        .Ldsn_run:
            call dsn_step
            j .Ldsn_run
        .section .note.GNU-stack,"",@progbits
        """;

    private const string MspStartup = """
        .cpu 430
        .section .text.start,"ax",@progbits
        .global _start
        _start:
            mov.w #__stack_top,r1
            mov.w #0x5a80,&0x0120
            call #dsn_init
        .Ldsn_run:
            call #dsn_step
            br #.Ldsn_run
        .section .resetvec,"a",@progbits
            .word _start
        """;

    private const string AdspStartup = """
        .EXTERN dsn_init;
        .EXTERN dsn_step;
        .SECTION/PM dsn_boot;
        JUMP dsn_start;
        .SECTION/PM dsn_code;
        .GLOBAL dsn_start;
        dsn_start:
        MSTAT = 0;
        IMASK = 0;
        CALL dsn_init;
        dsn_run:
        CALL dsn_step;
        JUMP dsn_run;
        """;
}
