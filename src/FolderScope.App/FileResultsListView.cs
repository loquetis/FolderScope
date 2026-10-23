using System.Runtime.InteropServices;

namespace FolderScope;

internal sealed class FileResultsListView : ListView
{
    private const int LvmGetTopIndex = 0x1027;
    private const int LvmSetExtendedListViewStyle = 0x1036;
    private const int LvsExDoubleBuffer = 0x00010000;

    private readonly ImageList _rowHeight = new() { ImageSize = new Size(1, 32), ColorDepth = ColorDepth.Depth32Bit };
    private FileHit[] _rows = [];
    private int _hotIndex = -1;
    private bool _layingOut;
    private DateField _field = DateField.Modified;

    public FileResultsListView()
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
        SortColumn = FileColumn.Size;
        SortDirection = FolderSortDirection.Descending;

        Columns.Add("Name", 240, HorizontalAlignment.Left);
        Columns.Add("Folder", 360, HorizontalAlignment.Left);
        Columns.Add("Size", 110, HorizontalAlignment.Right);
        Columns.Add("Last modified", 160, HorizontalAlignment.Right);
        RefreshHeaderText();
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

    public FileColumn SortColumn { get; private set; }

    public FolderSortDirection SortDirection { get; private set; }

    public int RowCount => _rows.Length;

    public FileHit[] Rows => _rows;

    public event EventHandler? RowActivated;

    public event EventHandler? SortChanged;

    public void SetDateField(DateField field)
    {
        if (_field == field)
            return;

        _field = field;
        RefreshHeaderText();
        Invalidate();
    }

    public bool TryGetSelected(out FileHit row)
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

    public FileHit[] SelectedRows()
    {
        var rows = new List<FileHit>();
        foreach (int index in SelectedIndices)
        {
            if ((uint)index < (uint)_rows.Length)
                rows.Add(_rows[index]);
        }

        return rows.ToArray();
    }

    public void SetRows(FileHit[] rows, string? reselectPath, bool preserveScroll) =>
        SetRows(rows, string.IsNullOrEmpty(reselectPath) ? null : [reselectPath], preserveScroll);

    public void SetRows(FileHit[] rows, IReadOnlyCollection<string>? reselectPaths, bool preserveScroll)
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
        item.SubItems.Add("");
        item.SubItems.Add("");
        item.SubItems.Add("");
        if ((uint)e.ItemIndex < (uint)_rows.Length)
            item.ToolTipText = _rows[e.ItemIndex].FullPath;
        e.Item = item;
    }

    protected override void OnColumnClick(ColumnClickEventArgs e)
    {
        var column = e.Column switch
        {
            0 => FileColumn.Name,
            1 => FileColumn.Path,
            2 => FileColumn.Size,
            _ => FileColumn.Date
        };

        if (column == SortColumn)
        {
            SortDirection = SortDirection == FolderSortDirection.Ascending
                ? FolderSortDirection.Descending
                : FolderSortDirection.Ascending;
        }
        else
        {
            SortColumn = column;
            SortDirection = column is FileColumn.Name or FileColumn.Path
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
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        if ((uint)e.ItemIndex >= (uint)_rows.Length)
            return;

        var row = _rows[e.ItemIndex];
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

        var state = e.Graphics.Save();
        e.Graphics.SetClip(e.Bounds);
        using (var brush = new SolidBrush(back))
            e.Graphics.FillRectangle(brush, e.Bounds);

        if (selected && e.ColumnIndex == 0 && !highContrast)
        {
            using var accent = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        var text = e.ColumnIndex switch
        {
            0 => row.DisplayName,
            1 => row.DirectoryPath,
            2 => SizeFormatter.Format(row.Size),
            _ => DateText.FormatLocal(row.SelectedUtc)
        };

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        var bounds = e.Bounds;
        if (e.ColumnIndex == 0)
        {
            var glyphSize = Math.Max(12, bounds.Height - Scale(16));
            var glyphY = bounds.Y + (bounds.Height - glyphSize) / 2;
            var glyph = new Rectangle(bounds.X + Scale(10), glyphY, glyphSize, glyphSize - Math.Max(0, glyphSize / 8));
            var kind = row.IsReparsePoint ? GlyphKind.Link : GlyphKind.File;
            Glyphs.Draw(e.Graphics, glyph, kind, highContrast ? fore : Theme.FileGlyph);
            bounds = new Rectangle(glyph.Right + Scale(8), bounds.Y, Math.Max(0, bounds.Right - glyph.Right - Scale(12)), bounds.Height);
            flags |= TextFormatFlags.Left;
        }
        else
        {
            bounds.Inflate(-8, 0);
            flags |= e.ColumnIndex == 2 ? TextFormatFlags.Right : TextFormatFlags.Left;
            if (!highContrast && e.ColumnIndex == 2 && row.Size == 0)
                fore = Theme.Muted;
            if (!highContrast && e.ColumnIndex == 3 && row.SelectedUtc == default)
                fore = Theme.Muted;
        }

        TextRenderer.DrawText(e.Graphics, text, Font, bounds, fore, flags);
        e.Graphics.Restore(state);
    }

    private void RefreshHeaderText()
    {
        string Mark(FileColumn column)
        {
            if (column != SortColumn)
                return "";
            return SortDirection == FolderSortDirection.Descending ? "  ▾" : "  ▴";
        }

        Columns[0].Text = "Name" + Mark(FileColumn.Name);
        Columns[1].Text = "Folder" + Mark(FileColumn.Path);
        Columns[2].Text = "Size" + Mark(FileColumn.Size);
        Columns[3].Text = DateText.FieldName(_field) + Mark(FileColumn.Date);
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
        if (_layingOut || Columns.Count < 4 || ClientSize.Width <= 0)
            return;

        _layingOut = true;
        try
        {
            Columns[0].Width = Scale(240);
            Columns[2].Width = Scale(110);
            Columns[3].Width = Scale(160);
            var used = Columns[0].Width + Columns[2].Width + Columns[3].Width;
            Columns[1].Width = Math.Max(Scale(180), ClientSize.Width - used - SystemInformation.VerticalScrollBarWidth - 4);
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
