// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Buffers.Binary;
using System.Text;
using System.Security.Cryptography;

namespace DSN.Core;

// Чтение таблиц размещения и каталога CFB без COM, OrCAD и сторонних DLL.
public sealed class CompoundFile
{
    private const uint End = 0xfffffffa;
    private readonly byte[] data;
    private readonly int sectorSize;
    private readonly uint[] fat;
    private readonly uint[] miniFat;
    private readonly byte[] miniStream;
    private readonly List<Entry> entries = [];
    private readonly Dictionary<string, Entry> streams = new(StringComparer.Ordinal);
    private sealed record Entry(string Name, byte Type, uint Left, uint Right, uint Child, uint Start, ulong Size);
    public IEnumerable<string> StreamNames => streams.Keys;
    public string ContentSha256 { get; }

    public CompoundFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length is < 512 or > 134217728) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text001"));
        data = File.ReadAllBytes(path);
        ContentSha256 = Convert.ToHexString(SHA256.HashData(data));
        if (!data.AsSpan(0, 8).SequenceEqual(Convert.FromHexString("D0CF11E0A1B11AE1")))
            throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text002"));
        if (U16(28) != 0xfffe || U16(30) is not (9 or 12) || U16(32) != 6)
            throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text003"));
        sectorSize = 1 << U16(30);
        var ids = Enumerable.Range(0, 109).Select(i => U32(76 + 4 * i)).ToList();
        var difat = U32(68);
        var seen = new HashSet<uint>();
        for (uint n = 0; n < U32(72); n++)
        {
            if (!seen.Add(difat)) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text004"));
            var words = Words(Sector(difat));
            ids.AddRange(words[..^1]); difat = words[^1];
        }
        fat = ids.Where(i => i < End).SelectMany(i => Words(Sector(i))).ToArray();
        var directory = Chain(U32(48), fat, false);
        for (var p = 0; p < directory.Length; p += 128)
        {
            var r = directory.AsSpan(p, 128);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(r[64..]);
            if (r[66] != 0 && (length < 2 || length > 64 || length % 2 != 0))
                throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text005"));
            entries.Add(new Entry(r[66] == 0 ? "" : Encoding.Unicode.GetString(r[..(length - 2)]), r[66],
                BinaryPrimitives.ReadUInt32LittleEndian(r[68..]), BinaryPrimitives.ReadUInt32LittleEndian(r[72..]),
                BinaryPrimitives.ReadUInt32LittleEndian(r[76..]), BinaryPrimitives.ReadUInt32LittleEndian(r[116..]),
                BinaryPrimitives.ReadUInt64LittleEndian(r[120..])));
        }
        if (entries.Count == 0 || entries[0].Type != 5) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text006"));
        miniStream = Chain(entries[0].Start, fat, false)[..CheckedSize(entries[0].Size)];
        miniFat = Words(Chain(U32(60), fat, false));
        Visit(entries[0].Child, "", new HashSet<uint>());
    }

    public byte[] Read(string name)
    {
        if (!streams.TryGetValue(name.Trim('/'), out var e)) throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Core.CompoundFile.Text007"), name));
        return Chain(e.Start, e.Size < 4096 ? miniFat : fat, e.Size < 4096)[..CheckedSize(e.Size)];
    }

    private void Visit(uint id, string parent, HashSet<uint> seen)
    {
        if (id >= End) return;
        if (id >= entries.Count || !seen.Add(id)) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text008"));
        var e = entries[(int)id];
        Visit(e.Left, parent, seen);
        var name = parent.Length == 0 ? e.Name : DSNText.Concat(DSNText.Concat(parent, "/"), e.Name);
        if (e.Type == 2 && !streams.TryAdd(name, e)) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text009"));
        if (e.Type == 1) Visit(e.Child, name, seen);
        Visit(e.Right, parent, seen);
    }

    private byte[] Chain(uint start, uint[] allocation, bool mini)
    {
        using var result = new MemoryStream();
        var seen = new HashSet<uint>();
        for (var i = start; i < End; i = allocation[(int)i])
        {
            if (i >= allocation.Length || !seen.Add(i)) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text010"));
            if (mini)
            {
                var p = checked((int)i * 64);
                if (p + 64 > miniStream.Length) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text011"));
                result.Write(miniStream, p, 64);
            }
            else result.Write(Sector(i));
            if (result.Length > 134217728) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text012"));
        }
        return result.ToArray();
    }

    private ReadOnlySpan<byte> Sector(uint id)
    {
        var p = checked(((long)id + 1) * sectorSize);
        if (p < 0 || p + sectorSize > data.Length) throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text013"));
        return data.AsSpan((int)p, sectorSize);
    }
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    {
        var result = new uint[bytes.Length / 4];
        for (var i = 0; i < result.Length; i++) result[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 * i)..]);
        return result;
    }
    private static int CheckedSize(ulong size) => size <= 134217728 ? (int)size : throw new InvalidDataException(DSNText.Russian("Core.CompoundFile.Text014"));
    private ushort U16(int p) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p));
    private uint U32(int p) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p));
}
