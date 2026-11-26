// Автор и правообладатель: Леонов Денис <466611@mail.ru>
namespace DSN.Core;

public sealed record ConnectionInfo(string Group, IReadOnlyList<string> Names);

public sealed class ConnectionGraph
{
    public Dictionary<string, ConnectionInfo> Ports { get; } = new(StringComparer.Ordinal);
    public Dictionary<uint, ConnectionInfo> Links { get; } = [];
    public List<string> Diagnostics { get; } = [];

    public static ConnectionGraph Build(LogicProgramDocument document, Page page)
    {
        var result = new ConnectionGraph();
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string key)
        {
            if (!parent.TryGetValue(key, out var value)) return parent[key] = key;
            return value == key ? key : parent[key] = Find(value);
        }
        void Join(string first, string second) { var a = Find(first); var b = Find(second); if (a != b) parent[a] = b; }
        static string Position(PointD point) => DSNText.Compose($"point:{point.X:R},{point.Y:R}");
        static string GroupKey(uint id) => DSNText.Concat("group:", id);
        var links = page.Links.ToDictionary(w => w.Id);
        var names = new List<(string Node, string Name)>();
        foreach (var link in page.Links)
        {
            Join(Position(link.Start), GroupKey(link.GroupId)); Join(Position(link.End), GroupKey(link.GroupId));
            foreach (var alias in link.Aliases) names.Add((GroupKey(link.GroupId), alias.Name));
        }
        foreach (var (id, name) in page.GroupNames) if (name.Length > 0) names.Add((GroupKey(id), name));
        foreach (var instance in page.Instances)
        {
            var symbol = document.Symbol(instance);
            if (instance.Orientation is not (0 or 6))
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Core.ConnectionGraph.Text001"), instance.Reference));
            if (instance.Ports.Count != symbol.Ports.Count)
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Core.ConnectionGraph.Text002"), instance.Reference));
            foreach (var port in instance.Ports)
            {
                if (port.Number < 1 || port.Number > symbol.Ports.Count) throw new InvalidDataException(DSNText.Russian("Core.ConnectionGraph.Text003"));
                var definition = symbol.Ports[port.Number - 1];
                if (port.Point != instance.Place(definition.Hot))
                    throw new InvalidDataException(DSNText.Russian("Core.ConnectionGraph.Text004", instance.Reference, definition.Name, instance.SymbolKey, instance.SourceLibrary, instance.Orientation, instance.Origin, port.Point, definition.Hot));
                var node = DSNText.Concat(DSNText.Concat(instance.Reference, "."), definition.Name);
                Join(node, Position(port.Point));
                if (port.LinkId != uint.MaxValue)
                {
                    if (!links.TryGetValue(port.LinkId, out var link)) throw new InvalidDataException(DSNText.Russian("Core.ConnectionGraph.Text005"));
                    Join(node, GroupKey(link.GroupId));
                }
                if (port.DirectGroupId > 0) Join(node, GroupKey(port.DirectGroupId));
            }
        }
        foreach (var externalPort in page.Ports)
        {
            var symbol = document.Symbol(externalPort);
            foreach (var port in symbol.Ports)
                names.Add((Position(externalPort.Place(port.Hot)), externalPort.Value));
        }
        var groupedNames = names.GroupBy(n => Find(n.Node)).ToDictionary(g => g.Key,
            g => (IReadOnlyList<string>)g.Select(n => n.Name).Where(n => n.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
        foreach (var link in page.Links)
        {
            var group = Find(GroupKey(link.GroupId));
            result.Links.Add(link.Id, new ConnectionInfo(group, groupedNames.TryGetValue(group, out var labels) ? labels : []));
        }
        foreach (var instance in page.Instances)
            foreach (var definition in document.Symbol(instance).Ports)
            {
                var key = DSNText.Concat(DSNText.Concat(instance.Reference, "."), definition.Name);
                var group = Find(key);
                result.Ports.Add(key, new ConnectionInfo(group, groupedNames.TryGetValue(group, out var labels) ? labels : []));
            }
        return result;
    }
}
