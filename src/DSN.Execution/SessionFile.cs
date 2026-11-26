// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DSN.Execution;

public static class SessionFile
{
    public const int MaxMetadataBytes = 16 * 1024 * 1024;
    public const int MaxArchiveBytes = 128 * 1024 * 1024;
    public const int MaxHistoryCells = 20_000_000;
    public const int MaxManualAssignments = 1_000_000;
    private static readonly JsonSerializerOptions Compact = new(DSNProgramBinder.JsonOptions)
    { WriteIndented = false, Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic) };
    private sealed class Envelope
    {
        [JsonRequired] public string Format { get; set; } = "DSNCompiler.Session";
        [JsonRequired] public int Version { get; set; } = 2;
        [JsonRequired] public SavedExecutionSession Data { get; set; } = null!;
        [JsonRequired] public string MetadataSha256 { get; set; } = "";
        [JsonRequired] public string MemorySha256 { get; set; } = "";
        [JsonRequired] public string HistorySha256 { get; set; } = "";
    }

    private static string Hash<T>(T data)
    {
        using var hash = SHA256.Create();
        using var output = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        JsonSerializer.Serialize(output, data, Compact); output.FlushFinalBlock();
        return Convert.ToHexString(hash.Hash!);
    }

    public static void Save(string path, ExecutionSession session, SessionViewState? view = null)
    {
        var data = session.CaptureSession(view); Bounds(data);
        var destination = Path.GetFullPath(path);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, true))
                {
                    var memoryHash = WriteBinary(archive, "memory.bin", output => WriteWords(output, data.Machine.Memory));
                    var historyHash = WriteBinary(archive, "history.bin", output =>
                    {
                        foreach (var row in data.History)
                        {
                            output.WriteByte(PackFlags(row.Flags)); WriteWords(output, row.Values);
                        }
                    });
                    var envelope = new Envelope { Data = data, MetadataSha256 = Hash(data), MemorySha256 = memoryHash, HistorySha256 = historyHash };
                    using var metadata = archive.CreateEntry("session.json", CompressionLevel.Optimal).Open();
                    JsonSerializer.Serialize(metadata, envelope, Compact);
                }
                if (file.Length > MaxArchiveBytes) throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text001"));
                file.Flush(true);
            }
            Read(temporary);
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static SavedExecutionSession Read(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxArchiveBytes) throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text002"));
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var expected = new[] { "history.bin", "memory.bin", "session.json" };
        if (!archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).SequenceEqual(expected))
            throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text003"));
        var metadata = archive.GetEntry("session.json")!;
        if (metadata.Length > MaxMetadataBytes) throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text004"));
        Envelope envelope;
        using (var source = metadata.Open())
        {
            var bytes = new byte[checked((int)metadata.Length)]; source.ReadExactly(bytes);
            if (source.ReadByte() != -1) throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text005"));
            envelope = ExecutionJson.Parse<Envelope>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'), MaxMetadataBytes);
        }
        if (envelope.Format != "DSNCompiler.Session" || envelope.Version != 2 || envelope.Data is null)
            throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text006"));
        var data = envelope.Data;
        Bounds(data);
        if (Hash(data) != envelope.MetadataSha256) throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text007"));
        var memory = archive.GetEntry("memory.bin")!; var history = archive.GetEntry("history.bin")!;
        if (memory.Length != 2L * data.Machine.MemoryWords || history.Length != data.HistoryCount * (1L + 2L * data.HistoryWords.Length))
            throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text008"));
        ReadBinary(memory, envelope.MemorySha256, input => data.Machine.Memory = ReadWords(input, data.Machine.MemoryWords));
        ReadBinary(history, envelope.HistorySha256, input =>
        {
            data.History = new SavedHistoryRow[data.HistoryCount];
            for (var i = 0; i < data.HistoryCount; i++)
            {
                var flags = input.ReadByte();
                if (flags < 0 || flags > 15) throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text009"));
                data.History[i] = new(ReadWords(input, data.HistoryWords.Length), new((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0));
            }
        });
        return data;
    }

    private static void Bounds(SavedExecutionSession data)
    {
        if (data.Machine is null || data.Machine.MemoryWords is < 1 or > 65536 || data.HistoryWords is null || data.HistoryWords.Length > 65536 ||
            data.HistoryCount is < 1 or > ExecutionSession.MaxTicks + 1 || (long)data.HistoryCount * data.HistoryWords.Length > MaxHistoryCells)
            throw new InvalidDataException(DSNText.Russian("Execution.SessionFile.Text010"));
        ProcessorHistory.Bounds(data.ProcessorHistory, data.Machine.Tick);
    }
    private static byte PackFlags(AdspFlags flags) => (byte)((flags.Zero ? 1 : 0) | (flags.Negative ? 2 : 0) | (flags.Overflow ? 4 : 0) | (flags.Carry ? 8 : 0));
    private static void WriteWords(Stream output, ushort[] words)
    {
        var bytes = new byte[checked(words.Length * 2)];
        for (var i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2, 2), words[i]);
        output.Write(bytes);
    }
    private static ushort[] ReadWords(Stream input, int count)
    {
        var bytes = new byte[checked(count * 2)]; input.ReadExactly(bytes);
        var result = new ushort[count];
        for (var i = 0; i < count; i++) result[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2));
        return result;
    }
    private static string WriteBinary(ZipArchive archive, string name, Action<Stream> write)
    {
        using var output = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        using var hash = SHA256.Create(); using var stream = new CryptoStream(output, hash, CryptoStreamMode.Write);
        write(stream); stream.FlushFinalBlock(); return Convert.ToHexString(hash.Hash!);
    }
    private static void ReadBinary(ZipArchiveEntry entry, string expectedHash, Action<Stream> read)
    {
        using var input = entry.Open(); using var hash = SHA256.Create(); using var stream = new CryptoStream(input, hash, CryptoStreamMode.Read);
        read(stream);
        if (stream.ReadByte() != -1 || Convert.ToHexString(hash.Hash!) != expectedHash) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.SessionFile.Text011"), entry.Name));
    }
}
