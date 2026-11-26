// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DSN.Core;
using DSN.Execution;
using CorePoint = DSN.Core.PointD;

namespace DSN.Viewer;

public sealed class LogicProgramCanvas : FrameworkElement
{
    private LogicProgramDocument? document;
    private Page? page;
    private double zoom = 1;
    private Vector pan;
    private Point? drag;
    private Instance? selected;
    private bool fullPage;
    private ExecutionSession? execution;
    private bool mirroredText;
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(28, 37, 43));
    private static readonly Brush LinkInk = new SolidColorBrush(Color.FromRgb(21, 97, 113));
    private static readonly Brush LabelInk = new SolidColorBrush(Color.FromRgb(35, 61, 85));
    public event Action<Instance?>? SelectionChanged;
    public event Action<double>? ZoomChanged;
    public bool ShowGrid { get; set; }
    public void SetExecution(ExecutionSession? session) { execution = session; InvalidateVisual(); }

    public LogicProgramCanvas()
    {
        ClipToBounds = true; Focusable = true; Cursor = Cursors.Cross;
        SizeChanged += (_, _) => { if (page is not null) Fit(fullPage); };
    }

    public void SetDocument(LogicProgramDocument value, Page selectedPage)
    {
        document = value; page = selectedPage; selected = null; execution = null; fullPage = false;
        Fit(false); InvalidateVisual();
    }

    public void Select(Instance? instance)
    {
        selected = instance; SelectionChanged?.Invoke(instance); InvalidateVisual();
    }

    public void Fit(bool showWholePage)
    {
        fullPage = showWholePage;
        if (page is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = showWholePage ? new Rect(0, 0, page.Width, page.Height) : ContentBounds();
        bounds.Inflate(24, 28);
        zoom = Math.Min((ActualWidth - 40) / bounds.Width, (ActualHeight - 40) / bounds.Height);
        zoom = Math.Clamp(zoom, 0.03, 12);
        pan = new Vector((ActualWidth - bounds.Width * zoom) / 2 - bounds.X * zoom,
                         (ActualHeight - bounds.Height * zoom) / 2 - bounds.Y * zoom);
        ZoomChanged?.Invoke(zoom); InvalidateVisual();
    }

    public void Zoom(double factor) => ZoomAt(factor, new Point(ActualWidth / 2, ActualHeight / 2));
    public void FocusInstance(Instance instance)
    {
        if (document is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = SymbolBounds(document.Symbol(instance));
        var center = instance.Place(new CorePoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        zoom = Math.Clamp(Math.Max(zoom, 1.6), 0.03, 12);
        pan = new Vector(ActualWidth / 2 - center.X * zoom, ActualHeight / 2 - center.Y * zoom);
        ZoomChanged?.Invoke(zoom); InvalidateVisual();
    }
    private void ZoomAt(double factor, Point center)
    {
        var world = new Point((center.X - pan.X) / zoom, (center.Y - pan.Y) / zoom);
        zoom = Math.Clamp(zoom * factor, 0.03, 12);
        pan = new Vector(center.X - world.X * zoom, center.Y - world.Y * zoom);
        ZoomChanged?.Invoke(zoom); InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        if (document is null || page is null) return;
        dc.PushTransform(new MatrixTransform(zoom, 0, 0, zoom, pan.X, pan.Y));
        if (ShowGrid && zoom >= 0.5)
        {
            var area = ContentBounds(); area.Inflate(40, 40);
            var grid = new SolidColorBrush(Color.FromRgb(224, 230, 233));
            for (var x = Math.Floor(area.Left / 10) * 10; x < area.Right; x += 10)
                for (var y = Math.Floor(area.Top / 10) * 10; y < area.Bottom; y += 10)
                    dc.DrawEllipse(grid, null, new Point(x, y), 0.45 / zoom, 0.45 / zoom);
        }
        foreach (var graphic in page.Graphics) DrawGraphic(dc, graphic);
        if (fullPage)
            foreach (var title in page.Titles) DrawInstance(dc, title);
        foreach (var link in page.Links)
        {
            var valueBinding = execution is not null && execution.Bound.Links.TryGetValue(link.Id, out var binding) ? binding : null;
            dc.DrawLine(new Pen(valueBinding is null ? LinkInk : ValueBrush(valueBinding), valueBinding is null ? 0.7 : 1.5), P(link.Start), P(link.End));
            foreach (var alias in link.Aliases) Text(dc, alias.Name, alias.Point.X, alias.Point.Y - 10, alias.Font, LabelInk);
        }
        foreach (var instance in page.Instances.Concat(page.Ports)) DrawInstance(dc, instance);
        foreach (var group in page.Links.SelectMany(w => new[] { (Point: w.Start, w.GroupId), (Point: w.End, w.GroupId) })
                     .GroupBy(x => (x.Point, x.GroupId)).Where(g => g.Count() >= 3))
            dc.DrawEllipse(LinkInk, null, P(group.Key.Point), 1.4, 1.4);
        dc.Pop();
        var annotated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var annotations = new List<Rect>();
        if (execution is not null)
            foreach (var instance in page.Instances)
                foreach (var port in document.Symbol(instance).Ports)
                    if (execution.Bound.Ports.TryGetValue(DSNText.Concat(DSNText.Concat(instance.Reference, "."), port.Name), out var valueBinding))
                    {
                        var hot = instance.Place(port.Hot);
                        var position = new Point(hot.X * zoom + pan.X, hot.Y * zoom + pan.Y);
                        var radius = Math.Clamp(zoom * 2, 0.6, 3);
                        dc.DrawEllipse(ValueBrush(valueBinding), null, position, radius, radius);
                        if (valueBinding.Word is null || !annotated.Add(valueBinding.Word) || zoom < 0.65) continue;
                        var value = execution.ReadValue(valueBinding);
                        var label = value is 0 or 1 ? value.ToString(CultureInfo.InvariantCulture) : DSNText.Concat("0x", value.ToString("X4", CultureInfo.InvariantCulture));
                        var formatted = new FormattedText(label, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, ValueBrush(valueBinding), 1);
                        var area = new Rect(position.X + 5, position.Y + 5, formatted.Width + 3, formatted.Height);
                        for (var attempt = 0; attempt < 5 && annotations.Any(r => r.IntersectsWith(area)); attempt++) area.Offset(0, formatted.Height + 1);
                        annotations.Add(area);
                        if (area.Y > position.Y + 6) dc.DrawLine(new Pen(Brushes.LightSlateGray, 0.5), position, area.TopLeft);
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)), null, area);
                        dc.DrawText(formatted, new Point(area.X + 1, area.Y));
                    }
    }

    private Brush ValueBrush(ValueBinding valueBinding) => execution!.ValueChanged(valueBinding) ? Brushes.DarkOrange : execution.ReadValue(valueBinding) switch
    { 0 => Brushes.SlateGray, 1 => Brushes.SeaGreen, _ => Brushes.RoyalBlue };

    private void DrawInstance(DrawingContext dc, Instance instance)
    {
        if (document is null || !document.Symbols.TryGetValue(instance.SymbolKey.Length > 0 ? instance.SymbolKey : instance.SymbolName, out var symbol)) return;
        dc.PushTransform(new TranslateTransform(instance.Origin.X, instance.Origin.Y));
        if (instance.Orientation == 6) dc.PushTransform(new MatrixTransform(1, 0, 0, -1, 0, instance.MirrorOffset));
        if (ReferenceEquals(instance, selected))
        {
            var bounds = SymbolBounds(symbol); bounds.Inflate(5, 5);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(38, 35, 126, 195)),
                new Pen(new SolidColorBrush(Color.FromRgb(35, 126, 195)), 1), bounds, 3, 3);
        }
        mirroredText = instance.Orientation == 6;
        foreach (var graphic in symbol.Graphics) DrawGraphic(dc, graphic);
        mirroredText = false;
        foreach (var port in symbol.Ports)
        {
            var start = P(port.Start); var hot = P(port.Hot);
            if (start == hot) continue;
            if ((port.Shape & 4) != 0)
            {
                var vector = hot - start; vector.Normalize();
                dc.DrawLine(new Pen(Ink, 0.6), hot, start + vector * 4);
                dc.DrawEllipse(Brushes.White, new Pen(Ink, 0.6), start + vector * 2, 2, 2);
            }
            else dc.DrawLine(new Pen(Ink, 0.6), hot, start);
        }
        if (instance.Orientation == 6) dc.Pop();
        foreach (var resolved in PropertyDisplay.Resolve(document, page!, symbol, instance))
        {
            var label = resolved.Layout;
            Text(dc, resolved.Text, label.X, label.Y, label.FontRotation & 0x3fff, LabelInk);
        }
        dc.Pop();
    }

    private void DrawGraphic(DrawingContext dc, Graphic g)
    {
        var pen = new Pen(Ink, g.LineWidth == 0 ? 0.65 : Math.Min(g.LineWidth, 2));
        if (g.LineStyle == 2) pen.DashStyle = DashStyles.Dash;
        var p = g.Points.Select(P).ToList();
        if (g.Kind == "Text") { Text(dc, g.Text, p[0].X, p[0].Y, g.Font, Ink); return; }
        if (g.Kind == "Line") { dc.DrawLine(pen, p[0], p[1]); return; }
        if (g.Kind == "Rect") { dc.DrawRectangle(null, pen, new Rect(p[0], p[1])); return; }
        if (g.Kind == "Ellipse")
        {
            var rect = new Rect(p[0], p[1]); dc.DrawEllipse(null, pen,
                new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2); return;
        }
        if (g.Kind is "Polygon" or "Polyline")
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            { context.BeginFigure(p[0], false, g.Kind == "Polygon"); context.PolyLineTo(p.Skip(1).ToArray(), true, false); }
            dc.DrawGeometry(null, pen, geometry); return;
        }
        if (g.Kind == "Arc")
        {
            var rect = new Rect(p[0], p[1]); var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            var a = Math.Atan2((p[2].Y - center.Y) / (rect.Height / 2), (p[2].X - center.X) / (rect.Width / 2));
            var b = Math.Atan2((p[3].Y - center.Y) / (rect.Height / 2), (p[3].X - center.X) / (rect.Width / 2));
            while (b <= a) b += Math.PI * 2;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(p[2], false, false);
                for (var t = a + 0.05; t < b; t += 0.05)
                    context.LineTo(new Point(center.X + rect.Width / 2 * Math.Cos(t), center.Y + rect.Height / 2 * Math.Sin(t)), true, false);
                context.LineTo(p[3], true, false);
            }
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    private void Text(DrawingContext dc, string text, double x, double y, int font, Brush brush)
    {
        if (text.Length == 0 || document is null) return;
        var spec = font > 0 && font <= document.Fonts.Count ? document.Fonts[font - 1] : document.Fonts[0];
        var typeface = new Typeface(new FontFamily(spec.Face), spec.Italic ? FontStyles.Italic : FontStyles.Normal,
            spec.Weight >= 600 ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight,
            typeface, Math.Clamp(Math.Abs(spec.Height), 5, 100), brush, 1);
        if (mirroredText) dc.PushTransform(new MatrixTransform(1, 0, 0, -1, 0, 2 * y + formatted.Height));
        dc.DrawText(formatted, new Point(x, y));
        if (mirroredText) dc.Pop();
    }

    private Rect ContentBounds()
    {
        if (page is null || document is null) return new Rect(0, 0, 100, 100);
        var points = page.Links.SelectMany(w => new[] { w.Start, w.End }).Concat(page.Graphics.SelectMany(g => g.Points)).ToList();
        foreach (var instance in page.Instances.Concat(page.Ports))
        {
            var bounds = SymbolBounds(document.Symbol(instance));
            var first = instance.Place(new CorePoint(bounds.Left, bounds.Top));
            var second = instance.Place(new CorePoint(bounds.Right, bounds.Bottom));
            points.Add(new CorePoint(Math.Min(first.X, second.X) - 60, Math.Min(first.Y, second.Y) - 25));
            points.Add(new CorePoint(Math.Max(first.X, second.X) + 30, Math.Max(first.Y, second.Y) + 20));
        }
        if (points.Count == 0) return new Rect(0, 0, 100, 100);
        return new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)), new Point(points.Max(p => p.X), points.Max(p => p.Y)));
    }

    private static Rect SymbolBounds(Symbol symbol)
    {
        var points = symbol.Graphics.SelectMany(g => g.Points).Concat(symbol.Ports.SelectMany(p => new[] { p.Start, p.Hot })).ToList();
        if (points.Count == 0) return new Rect(0, 0, 30, 40);
        return new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)), new Point(points.Max(p => p.X), points.Max(p => p.Y)));
    }

    private static Point P(CorePoint p) => new(p.X, p.Y);
    internal DrawingGroup FullDrawing()
    {
        var result = new DrawingGroup();
        var oldZoom = zoom; var oldPan = pan; var oldPage = fullPage; var oldSelected = selected; var oldExecution = execution;
        try
        {
            zoom = 1; pan = new(); fullPage = true; selected = null; execution = null;
            using var context = result.Open(); OnRender(context);
        }
        finally { zoom = oldZoom; pan = oldPan; fullPage = oldPage; selected = oldSelected; execution = oldExecution; }
        result.Freeze(); return result;
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e) { ZoomAt(e.Delta > 0 ? 1.15 : 1 / 1.15, e.GetPosition(this)); e.Handled = true; }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        var point = e.GetPosition(this);
        if (e.ChangedButton == MouseButton.Left && page is not null && document is not null)
        {
            var world = new Point((point.X - pan.X) / zoom, (point.Y - pan.Y) / zoom);
            var hit = page.Instances.LastOrDefault(i =>
            {
                var local = SymbolBounds(document.Symbol(i));
                var a = i.Place(new CorePoint(local.Left, local.Top)); var b = i.Place(new CorePoint(local.Right, local.Bottom));
                var bounds = new Rect(P(a), P(b)); bounds.Inflate(3, 3);
                return bounds.Contains(world);
            });
            if (hit is not null) { Select(hit); e.Handled = true; return; }
        }
        drag = point; CaptureMouse(); Cursor = Cursors.Hand; e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (drag is not Point previous) return;
        var current = e.GetPosition(this); pan += current - previous; drag = current; InvalidateVisual();
    }
    protected override void OnMouseUp(MouseButtonEventArgs e) { drag = null; ReleaseMouseCapture(); Cursor = Cursors.Cross; }
}
