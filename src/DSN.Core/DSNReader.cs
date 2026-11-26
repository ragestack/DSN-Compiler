// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Text;

namespace DSN.Core;

// Чтение записей формата OrCAD Capture 2.0.
// Разбор записей ограничен версией формата 2.0.
public sealed class DSNReader
{
    private readonly LogicProgramDocument document = new();
    private readonly Dictionary<(string Name, string Source), string> symbolKeys = [];
    private static string NormalizeSource(string source) => source.Replace('/', '\\').ToUpperInvariant();
    private DSNReader() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
    public static LogicProgramDocument Read(string file)
    {
        var reader = new DSNReader();
        var container = new CompoundFile(file);
        reader.document.SourceSha256 = container.ContentSha256;
        reader.document.FileName = Path.GetFileName(file);
        reader.ReadLibrary(new DSNBinary(container.Read("Library"), "Library"));
        reader.ReadCache(new DSNBinary(container.Read("Cache"), "Cache"));
        foreach (var name in container.StreamNames.Where(p => p.Contains("/Pages/", StringComparison.Ordinal)))
            reader.document.Pages.Add(reader.ReadPage(new DSNBinary(container.Read(name), name)));
        if (reader.document.Pages.Count == 0) throw new InvalidDataException(DSNText.Russian("Core.DSNReader.Text001"));
        foreach (var instance in reader.document.Pages.SelectMany(p => p.Instances.Concat(p.Ports).Concat(p.Titles)))
        {
            if (!reader.symbolKeys.TryGetValue((instance.SymbolName, NormalizeSource(instance.SourceLibrary)), out var key))
                throw new InvalidDataException(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Core.DSNReader.Text002"), instance.SymbolName), " / "), instance.SourceLibrary));
            instance.SymbolKey = key;
            if (instance.Orientation == 6 && instance.Ports.Count > 0)
            {
                var first = instance.Ports[0];
                if (first.Number < 1 || first.Number > reader.document.Symbol(instance).Ports.Count)
                    throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Core.DSNReader.Text003"), instance.Reference));
                instance.MirrorOffset = first.Point.Y - instance.Origin.Y + reader.document.Symbol(instance).Ports[first.Number - 1].Hot.Y;
            }
        }
        foreach (var page in reader.document.Pages) ConnectionGraph.Build(reader.document, page);
        return reader.document;
    }

    private void ReadLibrary(DSNBinary r)
    {
        var signature = Encoding.ASCII.GetString(r.Bytes(32));
        if (!signature.StartsWith("OrCAD Windows Design", StringComparison.Ordinal)) throw r.Error(DSNText.Russian("Core.DSNReader.Text004"));
        var major = r.U16(); var minor = r.U16();
        document.FormatVersion = DSNText.Concat(DSNText.Concat(major, "."), minor);
        if (major != 2 || minor != 0) throw r.Error(DSNText.Russian("Core.DSNReader.Text005"));
        r.Skip(12);
        var fonts = r.U16();
        if (fonts is < 1 or > 256) throw r.Error(DSNText.Russian("Core.DSNReader.Text006"));
        for (var i = 1; i < fonts; i++)
        {
            var height = r.I32(); var width = r.I32(); r.Skip(8);
            var weight = r.I32(); var italic = r.Byte() != 0; r.Skip(7);
            var face = Encoding.GetEncoding(1251).GetString(r.Bytes(32)).Split('\0')[0];
            document.Fonts.Add(new FontSpec(height, width, weight, italic, face));
        }
        if (r.U16() != 24) throw r.Error(DSNText.Russian("Core.DSNReader.Text007"));
        r.Skip(48 + 8);
        for (var i = 0; i < 8; i++) r.Text();
        ReadPageSettings(r);
        var count = r.U16();
        for (var i = 0; i < count; i++) document.Strings.Add(r.Text());
    }

    private static (double Width, double Height, uint ModifiedTimestamp) ReadPageSettings(DSNBinary r)
    {
        var start = r.Position;
        r.Skip(4);
        var modifiedTimestamp = r.U32();
        r.Skip(16);
        var width = r.U32(); var height = r.U32(); var pitch = r.U32();
        if (pitch == 0) throw r.Error(DSNText.Russian("Core.DSNReader.Text008"));
        r.Position = start + 156;
        return (10.0 * width / pitch, 10.0 * height / pitch, modifiedTimestamp);
    }

    private void ReadCache(DSNBinary r)
    {
        var group = 0;
        while (r.Position < r.Length)
        {
            var count = r.U16();
            if (count > 4096) throw r.Error(DSNText.Russian("Core.DSNReader.Text009"));
            for (var i = 0; i < count; i++)
            {
                var name = r.Text();
                var variants = r.U16();
                if (variants > 32) throw r.Error(DSNText.Russian("Core.DSNReader.Text010", name));
                // После имени следует число сохранённых вариантов, а не категория источника.
                for (var variant = 0; variant < variants; variant++)
                {
                    r.Text(); // Сохранённый путь библиотеки – метаданные.
                    var id = r.U32();
                    if (id != r.U32()) throw r.Error(DSNText.Russian("Core.DSNReader.Text011"));
                    var type = r.U16();
                    try
                    {
                        if (type is 24 or 34 or 35 or 64 or 75) ReadSymbol(r, type);
                        else if (type == 6) ReadPartCell(r);
                        else if (type == 31) ReadPackage(r);
                        else throw r.Error(DSNText.Concat(DSNText.Concat(DSNText.Concat(DSNText.Russian("Core.DSNReader.Text012"), type), " / "), name));
                    }
                    catch (InvalidDataException ex) { throw new InvalidDataException(DSNText.Compose($"Cache {name}: {ex.Message}"), ex); }
                }
            }
            group++;
        }
        r.End();
    }

    private static void ReadPartCell(DSNBinary r)
    {
        r.Prefix(6); r.Text(); r.Text();
        var views = r.U16();
        if (views is < 1 or > 2) throw r.Error(DSNText.Russian("Core.DSNReader.Text013"));
        for (var i = 0; i < views; i++) r.Text();
    }

    private static void ReadPackage(DSNBinary r)
    {
        r.Prefix(31);
        for (var i = 0; i < 5; i++) r.Text();
        var devices = r.U16();
        for (var i = 0; i < devices; i++)
        {
            r.Prefix(32); r.Text(); r.Text();
            var ports = r.U16();
            for (var j = 0; j < ports; j++)
            {
                var start = r.Position;
                if (r.I16() == -1) continue;
                r.Position = start; r.Text(); r.Skip(1);
            }
        }
    }

    private Symbol ReadSymbol(DSNBinary r, int type)
    {
        var defaults = r.Prefix(type);
        var symbol = new Symbol { Name = r.Text(), DefaultProperties = defaults };
        symbol.SourceLibrary = r.Text(); r.Skip(4);
        var count = r.U16();
        for (var i = 0; i < count; i++) symbol.Graphics.Add(ReadGraphic(r));
        r.Skip(8); // Границы символа в старом формате: координаты int16.
        var ports = r.U16();
        for (var i = 0; i < ports; i++) symbol.Ports.Add(ReadFunctionPort(r));
        ReadLabels(r, symbol.Labels);
        if (type == 24)
        {
            for (var i = 0; i < 4; i++) r.Text();
            r.Skip(2);
        }
        var identity = (symbol.Name, NormalizeSource(symbol.SourceLibrary));
        if (symbolKeys.TryGetValue(identity, out var previous))
        {
            if (System.Text.Json.JsonSerializer.Serialize(document.Symbols[previous]) != System.Text.Json.JsonSerializer.Serialize(symbol))
                throw r.Error(DSNText.Concat(DSNText.Russian("Core.DSNReader.Text014"), symbol.Name));
        }
        else
        {
            var key = symbol.Name; var variant = 1;
            while (document.Symbols.ContainsKey(key)) key = DSNText.Concat(DSNText.Concat(symbol.Name, "#"), (++variant));
            document.Symbols.Add(key, symbol); symbolKeys.Add(identity, key);
        }
        return symbol;
    }

    private FunctionPort ReadFunctionPort(DSNBinary r)
    {
        var type = r.Peek();
        if (type is not (26 or 27)) throw r.Error(DSNText.Russian("Core.DSNReader.Text015"));
        r.Prefix(type);
        var name = r.Text();
        var start = Point32(r); var hot = Point32(r); var shape = r.U16();
        r.Skip(2); var port = r.U32(); r.Skip(4);
        var labels = new List<DisplayProperty>(); ReadLabels(r, labels);
        return new FunctionPort(name, start, hot, shape, port);
    }

    private static void ReadLabels(DSNBinary r, List<DisplayProperty> result)
    {
        var count = r.U16();
        for (var i = 0; i < count; i++)
        {
            r.Prefix(39);
            var index = r.U16(); var x = r.I16(); var y = r.I16();
            var font = r.U16(); var color = r.Byte(); var display = r.U16(); r.Skip(1);
            result.Add(new DisplayProperty(index, x, y, font, color, display));
        }
    }

    private static Graphic ReadGraphic(DSNBinary r)
    {
        var type = r.Byte();
        if (type is 40 or 41 or 43)
        {
            var points = new List<PointD> { Point32(r), Point32(r) };
            var style = r.I32(); var width = r.I32();
            if (type != 41) r.Skip(8);
            return new Graphic(type == 40 ? "Rect" : type == 41 ? "Line" : "Ellipse", points, LineStyle: style, LineWidth: width);
        }
        if (type == 42)
        {
            var points = new List<PointD> { Point32(r), Point32(r), Point32(r), Point32(r) };
            var style = r.I32(); var width = r.I32();
            return new Graphic("Arc", points, LineStyle: style, LineWidth: width);
        }
        if (type is 44 or 45)
        {
            var style = r.I32(); var width = r.I32();
            if (type == 44) r.Skip(8);
            var count = r.U16(); var points = new List<PointD>();
            for (var i = 0; i < count; i++) points.Add(new PointD(r.I16(), r.I16()));
            return new Graphic(type == 44 ? "Polygon" : "Polyline", points, LineStyle: style, LineWidth: width);
        }
        if (type == 46)
        {
            var origin = Point32(r); r.Skip(16);
            var font = r.U16(); r.Skip(2);
            return new Graphic("Text", [origin], r.Text(), font);
        }
        throw r.Error(DSNText.Concat(DSNText.Russian("Core.DSNReader.Text016"), type));
    }

    private Page ReadPage(DSNBinary r)
    {
        r.Prefix(10);
        var page = new Page { Name = r.Text() }; r.Text();
        (page.Width, page.Height, page.ModifiedTimestamp) = ReadPageSettings(r);
        var titles = r.U16();
        for (var i = 0; i < titles; i++) page.Titles.Add(ReadGraphicInstance(r, 65));
        var groupDefinitions = r.U16();
        for (var i = 0; i < groupDefinitions; i++) { var id = r.U32(); page.GroupNames[id] = r.Text(); }
        if (r.U16() != 0) throw r.Error(DSNText.Russian("Core.DSNReader.Text017"));
        var groupNames = r.U16();
        for (var i = 0; i < groupNames; i++) { var name = r.Text(); page.GroupNames[r.U32()] = name; }
        var links = r.U16();
        for (var i = 0; i < links; i++) page.Links.Add(ReadLink(r));
        var instances = r.U16();
        for (var i = 0; i < instances; i++) page.Instances.Add(ReadInstance(r));
        var ports = r.U16();
        for (var i = 0; i < ports; i++) page.Ports.Add(ReadGraphicInstance(r, 23));
        for (var i = 0; i < 4; i++) if (r.U16() != 0) throw r.Error(DSNText.Russian("Core.DSNReader.Text018"));
        var graphics = r.U16();
        for (var i = 0; i < graphics; i++) ReadPageGraphic(r, page);
        for (var i = 0; i < 2; i++) if (r.U16() != 0) throw r.Error(DSNText.Russian("Core.DSNReader.Text019"));
        r.End(); return page;
    }

    private static void ReadPageGraphic(DSNBinary r, Page page)
    {
        var type = r.Peek();
        if (type is < 55 or > 61) throw r.Error(DSNText.Concat(DSNText.Russian("Core.DSNReader.Text020"), type));
        r.Prefix(type); r.Skip(8); r.Text(); r.Skip(4 + 8 + 4 + 4);
        var labels = new List<DisplayProperty>(); ReadLabels(r, labels);
        if (r.Byte() != 2) throw r.Error(DSNText.Russian("Core.DSNReader.Text021"));
        r.Prefix(2); r.Text(); r.Text(); r.Skip(4);
        var count = r.U16();
        for (var i = 0; i < count; i++) page.Graphics.Add(ReadGraphic(r));
        r.Skip(8);
    }

    private static Link ReadLink(DSNBinary r)
    {
        r.Prefix(20);
        var link = new Link { Id = r.U32(), GroupId = r.U32() }; r.Skip(4);
        link.Start = Point32(r); link.End = Point32(r); r.Skip(1);
        var aliases = r.U16();
        for (var i = 0; i < aliases; i++)
        {
            r.Prefix(49); var location = Point32(r); r.Skip(4);
            var rotation = r.I32(); var font = r.I32();
            link.Aliases.Add(new LinkLabel(r.Text(), location, font, rotation));
        }
        var labels = new List<DisplayProperty>(); ReadLabels(r, labels);
        return link;
    }

    private Instance ReadInstance(DSNBinary r)
    {
        var instance = new Instance { Properties = r.Prefix(13) };
        r.Skip(2); instance.SourceLibrary = document.String(r.U16()); r.Skip(4);
        instance.SymbolName = r.Text(); instance.Id = r.U32(); r.Skip(8);
        instance.Origin = new PointD(r.I16(), r.I16());
        r.Skip(1); instance.Orientation = r.Byte(); r.Skip(2); ReadLabels(r, instance.Labels); r.Skip(1);
        instance.Reference = r.Text(); r.Skip(8);
        var count = r.U16();
        for (var i = 0; i < count; i++)
        {
            r.Prefix(16); var number = r.U16(); var location = new PointD(r.I16(), r.I16()); var link = r.U32(); var connectionGroup = r.U32();
            var labels = new List<DisplayProperty>(); ReadLabels(r, labels);
            instance.Ports.Add(new PortConnection(number, location, link, connectionGroup));
        }
        instance.Value = r.Text(); r.Skip(2); return instance;
    }

    private Instance ReadGraphicInstance(DSNBinary r, int type)
    {
        var instance = new Instance { Properties = r.Prefix(type) };
        var nameIndex = r.U16(); instance.SourceLibrary = document.String(r.U16()); r.Skip(4); instance.Value = document.String(nameIndex);
        instance.SymbolName = r.Text(); instance.Id = r.U32(); r.Skip(8);
        instance.Origin = new PointD(r.I16(), r.I16());
        r.Skip(1); instance.Orientation = r.Byte(); r.Skip(2); ReadLabels(r, instance.Labels); r.Skip(1);
        r.Skip(type == 65 ? 12 : 9);
        return instance;
    }

    private static PointD Point32(DSNBinary r) => new(r.I32(), r.I32());
}
