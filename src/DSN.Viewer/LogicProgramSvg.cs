// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using DSN.Core;

namespace DSN.Viewer;

internal static class LogicProgramSvg
{
    private static readonly XNamespace ns = "http://www.w3.org/2000/svg";
    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    internal static string Create(LogicProgramDocument document)
    {
        var canvas = new LogicProgramCanvas(); canvas.SetDocument(document, document.Pages[0]);
        var drawing = canvas.FullDrawing(); var bounds = drawing.Bounds;
        if (bounds.IsEmpty || bounds.Width == 0 || bounds.Height == 0) bounds = new Rect(0, 0, 100, 100);
        bounds.Inflate(24, 24);
        var root = new XElement(ns + "svg", new XAttribute("viewBox", $"{N(bounds.X)} {N(bounds.Y)} {N(bounds.Width)} {N(bounds.Height)}"),
            new XAttribute("role", "img"), new XElement(ns + "title", System.IO.Path.GetFileName(document.FileName)));
        root.Add(Convert(drawing));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Convert(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
        {
            var result = new XElement(ns + "g"); var m = group.Transform?.Value ?? Matrix.Identity;
            if (!m.IsIdentity) result.SetAttributeValue("transform", $"matrix({N(m.M11)} {N(m.M12)} {N(m.M21)} {N(m.M22)} {N(m.OffsetX)} {N(m.OffsetY)})");
            if (group.Opacity != 1) result.SetAttributeValue("opacity", N(group.Opacity));
            foreach (var child in group.Children) result.Add(Convert(child));
            return result;
        }
        if (drawing is GeometryDrawing geometry)
        {
            var result = Path(geometry.Geometry); Brush(result, "fill", geometry.Brush);
            if (geometry.Pen is { } pen)
            {
                Brush(result, "stroke", pen.Brush); result.SetAttributeValue("stroke-width", N(pen.Thickness));
                result.SetAttributeValue("stroke-linejoin", pen.LineJoin.ToString().ToLowerInvariant());
                if (pen.DashStyle.Dashes.Count > 0) result.SetAttributeValue("stroke-dasharray", string.Join(" ", pen.DashStyle.Dashes.Select(d => N(d * pen.Thickness))));
            }
            return result;
        }
        if (drawing is GlyphRunDrawing glyph)
        {
            // Контуры шрифта взяты из того же рисунка WPF: внешний шрифт браузеру не нужен.
            var result = Path(glyph.GlyphRun.BuildGeometry()); Brush(result, "fill", glyph.ForegroundBrush); return result;
        }
        throw new InvalidOperationException(DSNText.Russian("Report.DrawingUnsupported"));
    }
    private static XElement Path(Geometry geometry)
    {
        var path = PathGeometry.CreateFromGeometry(geometry); var text = path.ToString(CultureInfo.InvariantCulture);
        if (text.StartsWith("F0", StringComparison.Ordinal) || text.StartsWith("F1", StringComparison.Ordinal)) text = text[2..].TrimStart();
        return new(ns + "path", new XAttribute("d", text), new XAttribute("fill-rule", path.FillRule == FillRule.EvenOdd ? "evenodd" : "nonzero"));
    }
    private static void Brush(XElement element, string name, Brush? brush)
    {
        if (brush is null) { element.SetAttributeValue(name, "none"); return; }
        if (brush is not SolidColorBrush solid) throw new InvalidOperationException(DSNText.Russian("Report.DrawingUnsupported"));
        var c = solid.Color; element.SetAttributeValue(name, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
        if (c.A < 255 || brush.Opacity < 1) element.SetAttributeValue(name + "-opacity", N(c.A / 255.0 * brush.Opacity));
    }
}
