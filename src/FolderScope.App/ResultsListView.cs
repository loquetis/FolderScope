using System.Runtime.InteropServices;

namespace FolderScope;

internal sealed class ResultsListView : ListView
{
    private const int LvmGetTopIndex = 0x1027;
    private const int LvmSetExtendedListViewStyle = 0x1036;
    private const int LvsExDoubleBuffer = 0x00010000;

    private readonly ImageList _rowHeight = new() { ImageSize = new Size(1, 32), ColorDepth = ColorDepth.Depth32Bit };
    private ListingRow[] _rows = [];
    private FolderColumn?[] _map = [];
    private string[] _titles = [];
    private int _hotIndex = -1;
    private bool _layingOut;
    private bool _showMatches;

    public ResultsListView()
    {
        VirtualMode = true;
        OwnerDraw = true;
        View = View.Details;
        FullRowSelect = true;
        MultiSelect = true;
        HideSelection = false;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Clickable;
        ShowItemToolTips = true;
        GridLines = false;
        HotTracking = false;
        LabelEdit = false;
        AllowColumnReorder = false;
        UseCompatibleStateImageBehavior = false;
        SmallImageList = _rowHeight;
        BackColor = Theme.Surface;
        ForeColor = Theme.Ink;
        SortColumn = FolderColumn.Size;
        SortDirection = FolderSortDirection.Descending;
        RebuildColumns();
        FitRowHeight();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitRowHeight();
    }

    private void FitRowHeight()
    {
        var height = Math.Max(28, Font.Height + 16);
        if (_rowHeight.ImageSize.Height != height)
            _rowHeight.ImageSize = new Size(1, height);
    }

    public FolderColumn SortColumn { get; private set; }

    public FolderSortDirection SortDirection { get; private set; }

    public long ParentSize { get; set; }

    public int RowCount => _rows.Length;

    public bool ShowMatches
    {
        get => _showMatches;
        set
        {
            if (_showMatches == value && Columns.Count > 0)
                return;

            _showMatches = value;
            RebuildColumns();
        }
    }

    public event EventHandler? RowActivated;

    public event EventHandler? SortChanged;

    public void InvalidateLive() => Invalidate();

    public bool TryGetSelected(out ListingRow row)
    {
        if (SelectedIndices.Count == 1)
        {
            var index = SelectedIndices[0];
            if ((uint)index < (uint)_rows.Length)
            {
                row = _rows[index];
                return true;
            }
        }

        row = default;
        return false;
    }

    public bool TryGetRow(int index, out ListingRow row)
    {
        if ((uint)index < (uint)_rows.Length)
        {
            row = _rows[index];
            return true;
        }

        row = default;
        return false;
    }

    public ListingRow[] SelectedRows()
    {
        var rows = new List<ListingRow>();
        foreach (int index in SelectedIndices)
        {
            if ((uint)index < (uint)_rows.Length)
                rows.Add(_rows[index]);
        }

        return rows.ToArray();
    }

    public void SetRows(ListingRow[] rows, string? reselectPath, bool preserveScroll) =>
        SetRows(rows, string.IsNullOrEmpty(reselectPath) ? null : [reselectPath], preserveScroll);

    public void SetRows(ListingRow[] rows, IReadOnlyCollection<string>? reselectPaths, bool preserveScroll)
    {
        var top = 0;
        if (preserveScroll && IsHandleCreated && VirtualListSize > 0)
            top = (int)SendMessage(Handle, LvmGetTopIndex, IntPtr.Zero, IntPtr.Zero);

        _rows = rows;
        BeginUpdate();
        try
        {
            VirtualListSize = _rows.Length;
            if (reselectPaths is { Count: > 0 })
            {
                SelectedIndices.Clear();
                for (var i = 0; i < _rows.Length; i++)
                {
                    foreach (var path in reselectPaths)
                    {
                        if (!string.Equals(_rows[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
                            continue;
                        SelectedIndices.Add(i);
                        break;
                    }
                }
            }

            if (preserveScroll && _rows.Length > 0 && top > 0)
            {
                var index = Math.Min(top, _rows.Length - 1);
                EnsureVisible(_rows.Length - 1);
                EnsureVisible(index);
            }
        }
        finally
        {
            EndUpdate();
        }

        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SendMessage(Handle, LvmSetExtendedListViewStyle, (IntPtr)LvsExDoubleBuffer, (IntPtr)LvsExDoubleBuffer);
        ApplyMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMetrics();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutColumns();
    }

    protected override void OnRetrieveVirtualItem(RetrieveVirtualItemEventArgs e)
    {
        var item = new ListViewItem("");
        for (var i = 1; i < Columns.Count; i++)
            item.SubItems.Add("");

        if ((uint)e.ItemIndex < (uint)_rows.Length)
            item.ToolTipText = RowText.Tooltip(_rows[e.ItemIndex]);

        e.Item = item;
    }

    protected override void OnColumnClick(ColumnClickEventArgs e)
    {
        if ((uint)e.Column >= (uint)_map.Length)
            return;

        var column = _map[e.Column] ?? FolderColumn.Size;
        if (column == SortColumn)
        {
            SortDirection = SortDirection == FolderSortDirection.Ascending
                ? FolderSortDirection.Descending
                : FolderSortDirection.Ascending;
        }
        else
        {
            SortColumn = column;
            SortDirection = column == FolderColumn.Name
                ? FolderSortDirection.Ascending
                : FolderSortDirection.Descending;
        }

        RefreshHeaderText();
        SortChanged?.Invoke(this, EventArgs.Empty);
        base.OnColumnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && SelectedIndices.Count == 1)
        {
            RowActivated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && HitTest(e.Location).Item != null)
            RowActivated?.Invoke(this, EventArgs.Empty);

        base.OnMouseDoubleClick(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        var index = hit.Item?.Index ?? -1;
        if (index != _hotIndex)
        {
            var previous = _hotIndex;
            _hotIndex = index;
            InvalidateRow(previous);
            InvalidateRow(index);
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hotIndex >= 0)
        {
            var previous = _hotIndex;
            _hotIndex = -1;
            InvalidateRow(previous);
        }

        base.OnMouseLeave(e);
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        var highContrast = SystemInformation.HighContrast;
        var back = highContrast ? SystemColors.Control : Theme.HeaderBack;
        var fore = highContrast ? SystemColors.ControlText : Theme.HeaderText;
        using (var brush = new SolidBrush(back))
            e.Graphics.FillRectangle(brush, e.Bounds);

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        flags |= e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
        var textBounds = Rectangle.Inflate(e.Bounds, -8, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", Font, textBounds, fore, flags);

        using var line = new Pen(highContrast ? SystemColors.ControlDark : Theme.Line);
        e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        // Sub-item drawing paints each cell, including the first column.
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        if ((uint)e.ItemIndex >= (uint)_rows.Length || (uint)e.ColumnIndex >= (uint)_map.Length)
            return;

        var row = _rows[e.ItemIndex];
        var column = _map[e.ColumnIndex];
        var selected = e.Item?.Selected == true;
        var hot = e.ItemIndex == _hotIndex && !selected;
        var highContrast = SystemInformation.HighContrast;
        Color back;
        Color fore;
        if (highContrast)
        {
            back = selected ? SystemColors.Highlight : SystemColors.Window;
            fore = selected ? SystemColors.HighlightText : SystemColors.WindowText;
        }
        else if (selected)
        {
            back = Theme.Selected;
            fore = Theme.Ink;
        }
        else if (hot)
        {
            back = Theme.Hover;
            fore = Theme.Ink;
        }
        else
        {
            back = e.ItemIndex % 2 == 0 ? Theme.Surface : Theme.RowAlt;
            fore = Theme.Ink;
        }

        if (row.AccessDenied && !highContrast)
            fore = Theme.Danger;

        var state = e.Graphics.Save();
        e.Graphics.SetClip(e.Bounds);
        using (var brush = new SolidBrush(back))
            e.Graphics.FillRectangle(brush, e.Bounds);

        if (selected && e.ColumnIndex == 0 && !highContrast)
        {
            using var accent = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        if (column == null)
        {
            DrawBar(e.Graphics, e.Bounds, row, highContrast);
        }
        else
        {
            var text = CellText(row, column.Value);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            var bounds = e.Bounds;
            if (column == FolderColumn.Name)
            {
                var glyphSize = Math.Max(12, bounds.Height - Scale(16));
                var glyphY = bounds.Y + (bounds.Height - glyphSize) / 2;
                var glyph = new Rectangle(bounds.X + Scale(10), glyphY, glyphSize, glyphSize - Math.Max(0, glyphSize / 8));
                var kind = row.IsReparsePoint
                    ? GlyphKind.Link
                    : row.IsDirectory ? GlyphKind.Folder : GlyphKind.File;
                var glyphColor = highContrast
                    ? fore
                    : row.AccessDenied
                        ? Theme.Danger
                        : row.IsDirectory ? Theme.FolderGlyph : Theme.FileGlyph;
                Glyphs.Draw(e.Graphics, glyph, kind, glyphColor);
                bounds = new Rectangle(glyph.Right + Scale(8), bounds.Y, Math.Max(0, bounds.Right - glyph.Right - Scale(12)), bounds.Height);
                flags |= TextFormatFlags.Left;
            }
            else
            {
                bounds.Inflate(-8, 0);
                flags |= TextFormatFlags.Right;
                if (!highContrast && IsMuted(row, column.Value))
                    fore = Theme.Muted;
            }

            TextRenderer.DrawText(e.Graphics, text, Font, bounds, fore, flags);
        }

        e.Graphics.Restore(state);
    }

    private string CellText(ListingRow row, FolderColumn column) =>
        column switch
        {
            FolderColumn.Name => row.DisplayName,
            FolderColumn.Size => SizeFormatter.Format(row.LiveSize),
            FolderColumn.Percent => SizeFormatter.FormatPercent(row.LiveSize, ParentSize),
            FolderColumn.Files => RowText.FilesCell(row),
            FolderColumn.Modified => RowText.ModifiedCell(row),
            FolderColumn.MatchSize => SizeFormatter.Format(row.MatchBytes),
            FolderColumn.MatchFiles => RowText.MatchFilesCell(row),
            _ => ""
        };

    private void DrawBar(Graphics graphics, Rectangle bounds, ListingRow row, bool highContrast)
    {
        var height = Math.Max(6, Scale(8));
        var track = new Rectangle(bounds.X + Scale(8), bounds.Y + (bounds.Height - height) / 2, Math.Max(0, bounds.Width - Scale(16)), height);
        var trackColor = highContrast ? SystemColors.Control : Theme.BarTrack;
        using (var brush = new SolidBrush(trackColor))
            graphics.FillRectangle(brush, track);

        var percent = ParentSize <= 0 ? 0d : Math.Clamp(row.LiveSize / (double)ParentSize, 0d, 1d);
        var fillWidth = (int)Math.Round(track.Width * percent);
        if (row.LiveSize > 0 && fillWidth < 2 && track.Width > 2)
            fillWidth = 2;
        if (fillWidth <= 0)
            return;

        var fillColor = highContrast
            ? SystemColors.Highlight
            : row.IsDirectory ? Theme.BarFill : Theme.FileBar;
        using var fill = new SolidBrush(fillColor);
        graphics.FillRectangle(fill, new Rectangle(track.X, track.Y, Math.Min(track.Width, fillWidth), track.Height));
    }

    private static bool IsMuted(ListingRow row, FolderColumn column) =>
        column switch
        {
            FolderColumn.Size or FolderColumn.Percent => row.LiveSize == 0,
            FolderColumn.MatchSize => row.MatchBytes == 0,
            FolderColumn.MatchFiles => row.MatchFiles == 0,
            FolderColumn.Modified => row.ModifiedUtc == default,
            _ => false
        };

    private void RebuildColumns()
    {
        if (IsHandleCreated)
            VirtualListSize = 0;

        var defs = new List<(FolderColumn? Column, string Title, int Width, HorizontalAlignment Align)>
        {
            (FolderColumn.Name, "Name", 320, HorizontalAlignment.Left),
            (FolderColumn.Size, "Size", 120, HorizontalAlignment.Right)
        };
        if (_showMatches)
            defs.Add((FolderColumn.MatchSize, "Match size", 124, HorizontalAlignment.Right));
        defs.Add((FolderColumn.Percent, "% of parent", 110, HorizontalAlignment.Right));
        defs.Add((FolderColumn.Files, "Files", 90, HorizontalAlignment.Right));
        if (_showMatches)
            defs.Add((FolderColumn.MatchFiles, "Match files", 110, HorizontalAlignment.Right));
        defs.Add((FolderColumn.Modified, "Modified", 150, HorizontalAlignment.Right));
        defs.Add((null, "Share", 150, HorizontalAlignment.Left));

        _map = defs.Select(def => def.Column).ToArray();
        _titles = defs.Select(def => def.Title).ToArray();
        _rows = [];

        BeginUpdate();
        try
        {
            Columns.Clear();
            foreach (var def in defs)
                Columns.Add(def.Title, def.Width, def.Align);
        }
        finally
        {
            EndUpdate();
        }

        if (!_showMatches && SortColumn is FolderColumn.MatchSize or FolderColumn.MatchFiles)
        {
            SortColumn = FolderColumn.Size;
            SortDirection = FolderSortDirection.Descending;
        }

        RefreshHeaderText();
        LayoutColumns();
    }

    private void RefreshHeaderText()
    {
        for (var i = 0; i < Columns.Count && i < _titles.Length; i++)
        {
            var mark = "";
            var column = _map[i];
            if (column != null && column == SortColumn)
                mark = SortDirection == FolderSortDirection.Descending ? "  ▾" : "  ▴";
            Columns[i].Text = _titles[i] + mark;
        }
    }

    private void ApplyMetrics()
    {
        var height = Math.Max(Scale(30), Font.Height + Scale(12));
        if (_rowHeight.ImageSize.Height != height)
            _rowHeight.ImageSize = new Size(1, height);
        LayoutColumns();
    }

    private void LayoutColumns()
    {
        if (_layingOut || Columns.Count != _map.Length || Columns.Count == 0 || ClientSize.Width <= 0)
            return;

        _layingOut = true;
        try
        {
            var used = 0;
            var nameIndex = 0;
            for (var i = 0; i < _map.Length; i++)
            {
                if (_map[i] == FolderColumn.Name)
                {
                    nameIndex = i;
                    continue;
                }

                var width = _map[i] switch
                {
                    FolderColumn.Size => Scale(112),
                    FolderColumn.MatchSize => Scale(124),
                    FolderColumn.Percent => Scale(108),
                    FolderColumn.Files => Scale(84),
                    FolderColumn.MatchFiles => Scale(112),
                    FolderColumn.Modified => Scale(148),
                    _ => Scale(128)
                };
                Columns[i].Width = width;
                used += width;
            }

            Columns[nameIndex].Width = Math.Max(Scale(180), ClientSize.Width - used - SystemInformation.VerticalScrollBarWidth - 4);
        }
        finally
        {
            _layingOut = false;
        }
    }

    private void InvalidateRow(int index)
    {
        if (!IsHandleCreated || index < 0 || index >= VirtualListSize)
            return;

        try
        {
            Invalidate(GetItemRect(index));
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    private int Scale(int logical) =>
        (int)Math.Round(logical * (DeviceDpi / 96.0));

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
