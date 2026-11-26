// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DSN.Execution;

namespace DSN.Viewer;

internal sealed class VariableTimelineView : DockPanel
{
    internal const int MaxVariables = 32;
    private readonly Plot plot;
    private readonly ScrollBar position = new() { Orientation = Orientation.Horizontal, SmallChange = 1, Minimum = 0, Height = 17 };
    private readonly TextBox cursorText = new() { Width = 65, Padding = new Thickness(3), Margin = new Thickness(3, 0, 3, 0) };
    private readonly CheckBox follow = UILocalization.With<global::System.Windows.Controls.CheckBox>(new() { Content = DSNText.Russian("Viewer.VariableTimelineView.Text001"), IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) }, ("Content", () => (object?)(DSNText.Russian("Viewer.VariableTimelineView.Text001"))));
    private readonly TextBlock notice = new() { TextWrapping = TextWrapping.Wrap, Padding = new Thickness(5, 2, 5, 3) };
    private readonly List<StateSample> samples = [];
    private string[] names = [];
    private StateSample? firstSample;
    private readonly Dictionary<string, Statistics> statistics = new(StringComparer.OrdinalIgnoreCase);
    private int counted;
    private int start;
    private int visible = 1;
    private int cursor;
    private bool fit = true;
    private bool updating;
    public int CursorTick => cursor;
    public int StartTick => start;
    public int EndTick => Math.Min(LastTick, start + visible - 1);
    public int DisplayedVariables => Math.Min(names.Length, MaxVariables);
    private int LastTick => Math.Max(0, samples.Count - 1);

    private sealed class Statistics { public bool Binary = true; public ushort Maximum; }

    public VariableTimelineView()
    {
        NameScope.SetNameScope(this, new NameScope());
        plot = new(this); UILocalization.Observe(this, () => plot.InvalidateVisual()); var toolbar = new WrapPanel { Margin = new Thickness(3) };
        AddButton(toolbar, DSNText.Russian("Viewer.VariableTimelineView.Text002"), "VariableTimelineFit", Fit);
        AddButton(toolbar, "+", "VariableTimelineZoomIn", () => Zoom(0.5));
        AddButton(toolbar, "−", "VariableTimelineZoomOut", () => Zoom(2));
        AddButton(toolbar, DSNText.Russian("Viewer.VariableTimelineView.Text003"), "VariableTimelineEnd", () => { follow.IsChecked = true; cursor = LastTick; start = Math.Max(0, samples.Count - visible); Update(); });
        toolbar.Children.Add(UILocalization.With<global::System.Windows.Controls.TextBlock>(new TextBlock { Text = DSNText.Russian("Viewer.VariableTimelineView.Text004"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) }, ("Text", () => (object?)(DSNText.Russian("Viewer.VariableTimelineView.Text004")))));
        cursorText.Name = "VariableTimelineCursorTick"; RegisterName(cursorText.Name, cursorText); toolbar.Children.Add(cursorText);
        AddButton(toolbar, DSNText.Russian("Viewer.VariableTimelineView.Text005"), "VariableTimelineGo", () =>
        {
            if (!int.TryParse(cursorText.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var tick) || tick < 0 || tick > LastTick)
            { UILocalization.Set(notice, "Text", () => DSNText.Russian("Viewer.VariableTimelineView.Text006", LastTick)); return; }
            SelectTick(tick);
        });
        follow.Name = "VariableTimelineFollow"; RegisterName(follow.Name, follow); toolbar.Children.Add(follow);
        position.Name = "VariableTimelinePosition"; RegisterName(position.Name, position);
        follow.Click += (_, _) => { if (follow.IsChecked == true) { cursor = LastTick; start = Math.Max(0, samples.Count - visible); } Update(); };
        SetDock(toolbar, Dock.Top); Children.Add(toolbar);
        SetDock(notice, Dock.Top); Children.Add(notice);
        SetDock(position, Dock.Bottom); Children.Add(position);
        position.ValueChanged += (_, _) => { if (!updating) { fit = false; follow.IsChecked = false; start = (int)Math.Round(position.Value); Update(); } };
        var scroll = new ScrollViewer { Content = plot, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Children.Add(scroll);
    }

    private void AddButton(Panel panel, string text, string name, Action action)
    {
        var button = UILocalization.With<global::System.Windows.Controls.Button>(new Button { Content = text, Name = name, Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(0, 0, 3, 0) }, ("Content", () => (object?)(text)));
        RegisterName(name, button);
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }

    public void SetData(IReadOnlyList<StateSample>? history, IEnumerable<string> selected)
    {
        var currentFirst = history is null || history.Count == 0 ? null : history[0];
        var changed = !ReferenceEquals(firstSample, currentFirst) || (history?.Count ?? 0) < counted;
        // Сеанс может очистить свой список до перерисовки. Снимок ссылок сохраняет согласованность истории и курсора; при обычном такте добавляем только новые строки.
        if (changed) samples.Clear();
        if (history is not null)
            for (var i = samples.Count; i < history.Count; i++) samples.Add(history[i]);
        if (changed) { firstSample = currentFirst; counted = 0; statistics.Clear(); fit = true; start = 0; cursor = LastTick; follow.IsChecked = true; }
        names = selected.ToArray();
        foreach (var name in names.Take(MaxVariables))
        {
            var exists = statistics.TryGetValue(name, out var info);
            if (!exists) statistics[name] = info = new();
            for (var i = exists ? counted : 0; i < samples.Count; i++)
            {
                var value = samples[i].Words[name]; info!.Binary &= value <= 1; info.Maximum = Math.Max(info.Maximum, value);
            }
        }
        foreach (var unused in statistics.Keys.Except(names.Take(MaxVariables), StringComparer.OrdinalIgnoreCase).ToArray()) statistics.Remove(unused);
        counted = samples.Count;
        if (fit) { start = 0; visible = Math.Max(1, samples.Count); }
        if (follow.IsChecked == true) { cursor = LastTick; if (!fit) start = Math.Max(0, samples.Count - visible); }
        start = Math.Clamp(start, 0, Math.Max(0, samples.Count - visible)); cursor = Math.Clamp(cursor, 0, LastTick);
        Update();
    }

    public void Fit() { fit = true; start = 0; visible = Math.Max(1, samples.Count); Update(); }
    public void Zoom(double factor)
    {
        fit = false; follow.IsChecked = false;
        var count = Math.Clamp((int)Math.Round(visible * factor), Math.Min(2, Math.Max(1, samples.Count)), Math.Max(1, samples.Count));
        var center = cursor >= start && cursor <= EndTick ? cursor : start + visible / 2;
        visible = count; start = Math.Clamp(center - visible / 2, 0, Math.Max(0, samples.Count - visible)); Update();
    }
    public void SelectTick(int tick)
    {
        if (tick < 0 || tick > LastTick) throw new ArgumentOutOfRangeException(nameof(tick));
        follow.IsChecked = false; cursor = tick;
        if (cursor < start || cursor > EndTick) start = Math.Clamp(cursor - visible / 2, 0, Math.Max(0, samples.Count - visible));
        Update();
    }
    public VariableTimelineViewState Capture(int historyTab) => new() { HistoryTab = historyTab, AutoFit = fit,
        FollowLatest = follow.IsChecked == true, StartTick = start, VisibleTicks = visible, CursorTick = cursor };
    public void Restore(VariableTimelineViewState? view)
    {
        if (view is null) { Fit(); follow.IsChecked = true; cursor = LastTick; Update(); return; }
        fit = view.AutoFit; follow.IsChecked = view.FollowLatest; visible = fit ? Math.Max(1, samples.Count) : view.VisibleTicks;
        start = Math.Clamp(view.StartTick, 0, Math.Max(0, samples.Count - visible)); cursor = Math.Clamp(view.CursorTick, 0, LastTick); Update();
    }
    public ushort CursorValue(string name) => VariableTimelineData.Value(samples, name, cursor);

    private void Update()
    {
        updating = true;
        position.Maximum = Math.Max(0, samples.Count - visible); position.LargeChange = Math.Max(1, visible - 1);
        position.ViewportSize = visible; position.Value = start; position.IsEnabled = position.Maximum > 0;
        updating = false;
        UILocalization.Set(cursorText, "Text", () => cursor.ToString(CultureInfo.InvariantCulture));
        UILocalization.Set(notice, "Text", () => samples.Count == 0 ? DSNText.Russian("Viewer.VariableTimelineView.Text007") : names.Length == 0 ?
            DSNText.Russian("Viewer.VariableTimelineView.Text008") :
            DSNText.Concat(DSNText.Russian("Viewer.VariableTimelineView.Text009", cursor, start, EndTick), (names.Length > MaxVariables ? DSNText.Russian("Viewer.VariableTimelineView.Text010", MaxVariables, names.Length) : "")));
        plot.Height = Math.Max(90, Plot.Axis + DisplayedVariables * Plot.Row + 8); plot.InvalidateVisual();
    }

    private sealed class Plot : FrameworkElement
    {
        public const double Axis = 28;
        public const double Row = 52;
        private readonly VariableTimelineView owner;
        private const double Left = 205;
        public Plot(VariableTimelineView owner)
        {
            this.owner = owner; ClipToBounds = true; Cursor = Cursors.Cross;
            MouseLeftButtonDown += (_, e) =>
            {
                var p = e.GetPosition(this);
                if (owner.samples.Count > 0 && p.X >= Left)
                    owner.SelectTick(VariableTimelineData.TickAt(p.X, Left, Math.Max(1, ActualWidth - Left - 15), owner.start, owner.EndTick));
            };
        }
        private static FormattedText Text(string text, double size, Brush brush) => new(DSNText.Translate(text), CultureInfo.GetCultureInfo(DSNText.Language == "en" ? "en-US" : "ru-RU"),
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1);
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc); dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
            if (owner.samples.Count == 0 || owner.names.Length == 0) return;
            var width = Math.Max(1, ActualWidth - Left - 15); var span = Math.Max(1, owner.EndTick - owner.start);
            double X(double tick) => Left + (tick - owner.start) * width / span;
            var grid = new Pen(Brushes.Gainsboro, 0.6);
            var step = Math.Max(1, (int)Math.Pow(10, Math.Floor(Math.Log10(span / Math.Max(1, width / 65)))));
            while (span / step > width / 55) step *= 2;
            for (var tick = (owner.start + step - 1) / step * step; tick <= owner.EndTick; tick += step)
            {
                var x = X(tick); dc.DrawLine(grid, new(x, Axis - 4), new(x, ActualHeight)); dc.DrawText(Text(tick.ToString(CultureInfo.InvariantCulture), 11, Brushes.DimGray), new(x + 2, 4));
            }
            for (var row = 0; row < owner.DisplayedVariables; row++)
            {
                var name = owner.names[row]; var top = Axis + row * Row; var info = owner.statistics[name];
                var color = (row % 3) switch { 0 => Brushes.Teal, 1 => Brushes.RoyalBlue, _ => Brushes.DarkViolet };
                var pen = new Pen(color, 1.5);
                dc.DrawRectangle(row % 2 == 0 ? Brushes.White : Brushes.WhiteSmoke, null, new Rect(0, top, Left - 2, Row));
                dc.PushClip(new RectangleGeometry(new Rect(4, top, Left - 10, Row)));
                var label = Text(name, 12, Brushes.Black); label.MaxTextWidth = Left - 12; label.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(label, new(5, top + 1));
                dc.DrawText(Text(ExecutionSession.FormatWord(owner.CursorValue(name)), 11, color), new(5, top + 19));
                dc.DrawText(Text(info.Binary ? "0/1" : DSNText.Concat(DSNText.Russian("Viewer.VariableTimelineView.Text011"), info.Maximum.ToString(CultureInfo.InvariantCulture)), 9, Brushes.DimGray), new(5, top + 36)); dc.Pop();
                var high = top + 7; var low = top + Row - 9; var max = Math.Max(1, (int)info.Maximum);
                double Y(ushort value) => low - value * (low - high) / max;
                dc.DrawLine(grid, new(Left, low), new(Left + width, low));
                var buckets = VariableTimelineData.Buckets(owner.samples, name, owner.start, owner.EndTick, Math.Max(1, (int)width));
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(new(X(owner.start), Y(buckets[0].First)), false, false);
                    var previous = buckets[0].First;
                    foreach (var bucket in buckets)
                    {
                        var x = X(bucket.FirstTick);
                        path.LineTo(new(x, Y(previous)), true, false);
                        path.LineTo(new(x, Y(bucket.First)), true, false);
                        if (bucket.Minimum != bucket.Maximum) { path.LineTo(new(x, Y(bucket.Minimum)), true, false); path.LineTo(new(x, Y(bucket.Maximum)), true, false); }
                        path.LineTo(new(x, Y(bucket.Last)), true, false);
                        path.LineTo(new(X(bucket.LastTick), Y(bucket.Last)), true, false);
                        previous = bucket.Last;
                    }
                    path.LineTo(new(X(owner.EndTick), Y(buckets[^1].Last)), true, false);
                    if (owner.start == owner.EndTick) path.LineTo(new(Left + width, Y(buckets[^1].Last)), true, false);
                }
                dc.DrawGeometry(null, pen, geometry);
                if (owner.cursor >= owner.start && owner.cursor <= owner.EndTick)
                    dc.DrawEllipse(color, null, new(X(owner.cursor), Y(owner.CursorValue(name))), 2.5, 2.5);
                dc.DrawLine(grid, new(0, top + Row), new(ActualWidth, top + Row));
            }
            if (owner.cursor >= owner.start && owner.cursor <= owner.EndTick)
                dc.DrawLine(new Pen(Brushes.DarkOrange, 1), new(X(owner.cursor), Axis - 4), new(X(owner.cursor), ActualHeight));
        }
    }
}
