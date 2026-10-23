namespace FolderScope;

internal sealed class CompositionBar : Control
{
    private readonly ToolTip _tooltip = new() { InitialDelay = 350, ReshowDelay = 120, AutoPopDelay = 8000 };
    private Segment[] _segments = [];
    private string? _lastTip;

    public CompositionBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        FitHeight();
        AccessibleName = "Share of this folder";
        BackColor = Theme.Window;
    }

    public event EventHandler<ListingRow>? SegmentActivated;

    public override Size GetPreferredSize(Size proposedSize) =>
        new(Math.Max(0, proposedSize.Width), Math.Max(22, Font.Height + 14));

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitHeight();
    }

    private void FitHeight() => Height = Math.Max(22, Font.Height + 14);

    public void Clear()
    {
        _segments = [];
        _lastTip = null;
        _tooltip.SetToolTip(this, "");
        Invalidate();
    }

    public void SetRows(IReadOnlyList<ListingRow> rows, long total)
    {
        _segments = Build(rows, total);
        _lastTip = null;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var bar = BarBounds();
        var highContrast = SystemInformation.HighContrast;
        using (var track = new SolidBrush(highContrast ? SystemColors.Control : Theme.BarTrack))
            e.Graphics.FillRectangle(track, bar);

        foreach (var (bounds, segment) in LayoutSegments(bar))
        {
            if (bounds.Width <= 0)
                continue;
            using var brush = new SolidBrush(highContrast ? SystemColors.Highlight : segment.Color);
            e.Graphics.FillRectangle(brush, bounds);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var tip = TipAt(e.Location);
        if (tip == _lastTip)
            return;
        _lastTip = tip;
        _tooltip.SetToolTip(this, tip ?? "");
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left)
            return;
        if (Hit(e.Location) is not { } segment || segment.Row is not { } row)
            return;
        if (!row.IsDirectory || row.IsReparsePoint)
            return;
        SegmentActivated?.Invoke(this, row);
    }

    private string? TipAt(Point location)
    {
        if (Hit(location) is not { } segment)
            return null;
        if (segment.IsOther || segment.Row is not { } row)
            return "Other  ·  " + SizeFormatter.Format(segment.Size);

        return row.Name + "  ·  " + SizeFormatter.Format(segment.Size);
    }

    private Segment? Hit(Point location)
    {
        foreach (var (bounds, segment) in LayoutSegments(BarBounds()))
        {
            if (bounds.Contains(location))
                return segment;
        }

        return null;
    }

    private Rectangle BarBounds()
    {
        var height = Math.Max(8, (int)Math.Round(10 * (DeviceDpi / 96.0)));
        var y = (ClientSize.Height - height) / 2;
        return new Rectangle(0, y, Math.Max(0, ClientSize.Width), height);
    }

    private IEnumerable<(Rectangle Bounds, Segment Segment)> LayoutSegments(Rectangle area)
    {
        if (_segments.Length == 0 || area.Width <= 0)
            yield break;

        long sum = 0;
        foreach (var segment in _segments)
            sum += segment.Size;
        if (sum <= 0)
            yield break;

        var x = area.X;
        var remaining = area.Width;
        for (var i = 0; i < _segments.Length; i++)
        {
            var width = i == _segments.Length - 1
                ? remaining
                : (int)Math.Round(area.Width * (_segments[i].Size / (double)sum));
            width = Math.Clamp(width, 0, remaining);
            yield return (new Rectangle(x, area.Y, width, area.Height), _segments[i]);
            x += width;
            remaining -= width;
        }
    }

    private static Segment[] Build(IReadOnlyList<ListingRow> rows, long total)
    {
        var ordered = new List<ListingRow>();
        foreach (var row in rows)
        {
            if (row.SortSize > 0)
                ordered.Add(row);
        }

        ordered.Sort((a, b) => b.SortSize.CompareTo(a.SortSize));
        var take = Math.Min(7, ordered.Count);
        long topSum = 0;
        var segments = new List<Segment>(take + 1);
        for (var i = 0; i < take; i++)
        {
            topSum += ordered[i].SortSize;
            segments.Add(new Segment(ordered[i], ordered[i].SortSize, Theme.Segments[i], isOther: false));
        }

        var other = total - topSum;
        if (other > 0)
            segments.Add(new Segment(null, other, Theme.Other, isOther: true));

        return segments.ToArray();
    }

    private readonly struct Segment
    {
        public Segment(ListingRow? row, long size, Color color, bool isOther)
        {
            Row = row;
            Size = size;
            Color = color;
            IsOther = isOther;
        }

        public ListingRow? Row { get; }

        public long Size { get; }

        public Color Color { get; }

        public bool IsOther { get; }
    }
}
