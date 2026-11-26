// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Buffers.Binary;
using System.Text;

namespace DSN.Core;

internal sealed class DSNBinary(byte[] bytes, string stream)
{
    public int Position { get; set; }
    public int Length => bytes.Length;
    public string Stream { get; } = stream;
    public byte Peek() { Need(1); return bytes[Position]; }
    public byte Byte() { Need(1); return bytes[Position++]; }
    public ushort U16() { Need(2); var v = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(Position)); Position += 2; return v; }
    public short I16() => unchecked((short)U16());
    public uint U32() { Need(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Position)); Position += 4; return v; }
    public int I32() => unchecked((int)U32());
    public byte[] Bytes(int count) { Need(count); var v = bytes.AsSpan(Position, count).ToArray(); Position += count; return v; }
    public void Skip(int count) { Need(count); Position += count; }
    public string Text()
    {
        var count = U16();
        if (count > 16384) throw Error("String length exceeds limit");
        var value = Encoding.GetEncoding(1251).GetString(Bytes(count));
        if (Byte() != 0) throw Error("String has no zero terminator");
        return value;
    }
    public Dictionary<ushort, ushort> Prefix(int expected)
    {
        var tag = Byte();
        if (tag != expected) throw Error(DSNText.Compose($"Expected record {expected}, found {tag}"));
        var count = I16();
        if (count < -1 || count > 4096) throw Error("Invalid property count");
        var result = new Dictionary<ushort, ushort>();
        for (var n = 0; n < count; n++) result[U16()] = U16();
        return result;
    }
    public void End() { if (Position != Length) throw Error(DSNText.Compose($"Unconsumed bytes: {Length - Position}")); }
    public InvalidDataException Error(string message) => new(DSNText.Compose($"{Stream} @ 0x{Position:X}: {message}"));
    private void Need(int count) { if (count < 0 || Position + (long)count > Length) throw Error("Unexpected end of stream"); }
}
