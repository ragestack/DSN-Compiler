// Автор и правообладатель: Леонов Денис <466611@mail.ru>
namespace DSN.Core;

public sealed record PointD(double X, double Y);
public sealed record FontSpec(int Height, int Width, int Weight, bool Italic, string Face);
public sealed record DisplayProperty(uint Index, double X, double Y, ushort FontRotation, byte Color, ushort Display);
public sealed record Graphic(string Kind, List<PointD> Points, string Text = "", int Font = 0, int LineStyle = 0, int LineWidth = 1);
public sealed record FunctionPort(string Name, PointD Start, PointD Hot, ushort Shape, uint KindCode);
public sealed class Symbol
{
    public string Name { get; set; } = "";
    public string SourceLibrary { get; set; } = "";
    public Dictionary<ushort, ushort> DefaultProperties { get; set; } = [];
    public List<Graphic> Graphics { get; } = [];
    public List<FunctionPort> Ports { get; } = [];
    public List<DisplayProperty> Labels { get; } = [];
}
public sealed record PortConnection(int Number, PointD Point, uint LinkId, uint DirectGroupId);
public sealed class Instance
{
    public string SymbolName { get; set; } = "";
    public string SymbolKey { get; set; } = "";
    public string SourceLibrary { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Value { get; set; } = "";
    public uint Id { get; set; }
    public PointD Origin { get; set; } = new(0, 0);
    public byte Orientation { get; set; }
    public double MirrorOffset { get; set; }
    public PointD Place(PointD point) => Orientation switch
    {
        0 => new(Origin.X + point.X, Origin.Y + point.Y),
        6 => new(Origin.X + point.X, Origin.Y + MirrorOffset - point.Y),
        _ => throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Core.Model.Text001"), Reference))
    };
    public Dictionary<ushort, ushort> Properties { get; set; } = [];
    public List<DisplayProperty> Labels { get; } = [];
    public List<PortConnection> Ports { get; } = [];
}
public sealed record LinkLabel(string Name, PointD Point, int Font, int Rotation);
public sealed class Link
{
    public uint Id { get; set; }
    public uint GroupId { get; set; }
    public PointD Start { get; set; } = new(0, 0);
    public PointD End { get; set; } = new(0, 0);
    public List<LinkLabel> Aliases { get; } = [];
}
public sealed class Page
{
    public string Name { get; set; } = "";
    public uint ModifiedTimestamp { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public List<Link> Links { get; } = [];
    public List<Instance> Instances { get; } = [];
    public List<Instance> Ports { get; } = [];
    public List<Instance> Titles { get; } = [];
    public List<Graphic> Graphics { get; } = [];
    public Dictionary<uint, string> GroupNames { get; } = [];
}
public sealed class LogicProgramDocument
{
    public string SourceSha256 { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FormatVersion { get; set; } = "";
    public List<string> Strings { get; } = [];
    public List<FontSpec> Fonts { get; } = [];
    public Dictionary<string, Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    public List<Page> Pages { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public string String(uint index) => index < Strings.Count ? Strings[(int)index] : "";
    public Symbol Symbol(Instance instance) => Symbols[instance.SymbolKey.Length > 0 ? instance.SymbolKey : instance.SymbolName];
}
