namespace FolderScope;

internal static class FormScale
{
    public static void Apply(Form form)
    {
        form.AutoScaleDimensions = new SizeF(96f, 96f);
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }
}

internal sealed class WrapFlow : FlowLayoutPanel
{
    private bool _fitting;

    public WrapFlow()
    {
        WrapContents = true;
        FlowDirection = FlowDirection.LeftToRight;
        AutoSize = false;
        Margin = Padding.Empty;
        Padding = new Padding(8, 6, 8, 6);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        if (width <= 0)
            width = 640;
        return new Size(width, Measure(width));
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_fitting || Controls.Count == 0)
            return;

        var bottom = 0;
        foreach (Control child in Controls)
        {
            if (!child.Visible)
                continue;
            bottom = Math.Max(bottom, child.Bottom + child.Margin.Bottom);
        }

        bottom += Padding.Bottom;
        if (bottom > Height + 1)
        {
            _fitting = true;
            Height = bottom;
            _fitting = false;
        }
    }

    private int Measure(int width)
    {
        var inner = Math.Max(1, width - Padding.Horizontal);
        var x = 0;
        var y = Padding.Vertical;
        var row = 0;
        var any = false;
        foreach (Control child in Controls)
        {
            if (!child.Visible)
                continue;

            any = true;
            var pref = child.GetPreferredSize(Size.Empty);
            var w = Math.Max(child.Width, pref.Width);
            var h = Math.Max(child.Height, pref.Height);
            if (child.MinimumSize.Width > 0)
                w = Math.Max(w, child.MinimumSize.Width);
            if (child.MinimumSize.Height > 0)
                h = Math.Max(h, child.MinimumSize.Height);
            w += child.Margin.Horizontal;
            h += child.Margin.Vertical;
            if (x > 0 && x + w > inner)
            {
                y += row;
                x = 0;
                row = 0;
            }

            x += w;
            row = Math.Max(row, h);
        }

        if (!any)
            return 0;

        return y + row;
    }
}

internal sealed class StackedChrome : Panel
{
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        if (width < 1)
            width = 1;
        var y = Padding.Vertical;
        var any = false;
        foreach (Control child in Controls)
        {
            if (!child.Visible)
                continue;
            any = true;
            var inner = Math.Max(1, width - Padding.Horizontal - child.Margin.Horizontal);
            var pref = child.GetPreferredSize(new Size(inner, 0));
            var h = Math.Max(pref.Height, child.Height);
            if (child.MinimumSize.Height > 0)
                h = Math.Max(h, child.MinimumSize.Height);
            y += h + child.Margin.Vertical;
        }

        return new Size(width, any ? y : 0);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        var y = Padding.Top;
        foreach (Control child in Controls)
        {
            if (!child.Visible)
                continue;
            var inner = Math.Max(1, width - child.Margin.Horizontal);
            var pref = child.GetPreferredSize(new Size(inner, 0));
            var h = Math.Max(pref.Height, child.MinimumSize.Height);
            if (h < 1)
                h = Math.Max(child.Height, 1);
            child.SetBounds(Padding.Left + child.Margin.Left, y + child.Margin.Top, inner, h);
            y = child.Bottom + child.Margin.Bottom;
        }
    }
}

internal sealed class CommandStrip : Panel
{
    private readonly FlowLayoutPanel _flow = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(8, 12, 8, 8),
        BackColor = Theme.Window
    };

    public CommandStrip()
    {
        BackColor = Theme.Window;
        Controls.Add(_flow);
    }

    public FlowLayoutPanel Flow => _flow;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = _flow.Padding.Horizontal + 4;
        foreach (Control child in _flow.Controls)
        {
            if (!child.Visible)
                continue;
            var pref = child.GetPreferredSize(Size.Empty);
            width = Math.Max(width, pref.Width + child.Margin.Horizontal + _flow.Padding.Horizontal);
            width = Math.Max(width, child.MinimumSize.Width + child.Margin.Horizontal + _flow.Padding.Horizontal);
        }

        return new Size(Math.Max(width, 56), 10);
    }
}

internal sealed class EllipsisLabel : Label
{
    private readonly ToolTip _tooltip = new() { InitialDelay = 350, ReshowDelay = 120, AutoPopDelay = 12000 };
    private string _full = "";

    public EllipsisLabel()
    {
        AutoSize = false;
        AutoEllipsis = false;
        UseMnemonic = false;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);
    }

    private string _tipText = "";

    public string TipText
    {
        get => _tipText;
        set
        {
            _tipText = value ?? "";
            UpdateTip();
        }
    }

    public bool RightAlign { get; set; }

    public string FullText => _full;

    public new string Text
    {
        get => _full;
        set
        {
            _full = value ?? "";
            base.Text = _full;
            UpdateTip();
            Invalidate();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var measured = TextRenderer.MeasureText(
            "Ay",
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return new Size(Math.Max(0, proposedSize.Width), measured.Height + 8);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateTip();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateTip();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var brush = new SolidBrush(BackColor))
            e.Graphics.FillRectangle(brush, ClientRectangle);

        var flags = TextFormatFlags.SingleLine
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.NoPadding;
        if (RightAlign)
            flags |= TextFormatFlags.Right | TextFormatFlags.EndEllipsis;
        else if (_full.Contains('\\') || _full.Contains('/'))
            flags |= TextFormatFlags.Left | TextFormatFlags.PathEllipsis;
        else
            flags |= TextFormatFlags.Left | TextFormatFlags.EndEllipsis;

        var rect = ClientRectangle;
        if (rect.Width > 6)
            rect.Inflate(-2, 0);
        TextRenderer.DrawText(e.Graphics, _full, Font, rect, ForeColor, flags);
    }

    private void UpdateTip()
    {
        var tip = TipText;
        if (string.IsNullOrEmpty(tip) && IsTruncated())
            tip = _full;
        _tooltip.SetToolTip(this, tip ?? "");
        if (!string.IsNullOrEmpty(_full))
            AccessibleDescription = string.IsNullOrEmpty(TipText) ? _full : TipText;
    }

    private bool IsTruncated()
    {
        if (string.IsNullOrEmpty(_full) || ClientSize.Width < 8)
            return false;

        var width = TextRenderer.MeasureText(
            _full,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        return width > ClientSize.Width - 4;
    }
}

internal sealed class StatusLine : Panel
{
    public StatusLine()
    {
        BackColor = Theme.Surface;
        Margin = new Padding(0, 0, 0, 4);
        LeftText.Dock = DockStyle.None;
        RightText.Dock = DockStyle.None;
        LeftText.BackColor = Theme.Surface;
        RightText.BackColor = Theme.Surface;
        LeftText.ForeColor = Theme.Ink;
        RightText.ForeColor = Theme.Muted;
        RightText.RightAlign = true;
        Controls.Add(LeftText);
        Controls.Add(RightText);
    }

    public EllipsisLabel LeftText { get; } = new();

    public EllipsisLabel RightText { get; } = new();

    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = Math.Max(
            LeftText.GetPreferredSize(Size.Empty).Height,
            RightText.GetPreferredSize(Size.Empty).Height);
        return new Size(Math.Max(0, proposedSize.Width), height);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        var rightNeed = string.IsNullOrEmpty(RightText.Text)
            ? 0
            : TextRenderer.MeasureText(RightText.Text, RightText.Font, new Size(int.MaxValue, int.MaxValue), flags).Width + 12;
        var cap = Math.Max(48, (int)(ClientSize.Width * 0.52));
        var rightWidth = Math.Min(rightNeed, cap);
        var leftWidth = Math.Max(0, ClientSize.Width - rightWidth);
        LeftText.SetBounds(0, 0, leftWidth, ClientSize.Height);
        RightText.SetBounds(leftWidth, 0, rightWidth, ClientSize.Height);
    }
}

internal static class LayoutAudit
{
    public static List<string> Collect(Control root)
    {
        var issues = new List<string>();
        Walk(root, issues);
        return issues;
    }

    private static void Walk(Control parent, List<string> issues)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible)
                continue;

            if (IsChrome(child) && IsClipped(parent, child))
                issues.Add(Describe(parent, child));

            if (child is Button button && ButtonTextClipped(button))
                issues.Add(Describe(parent, child) + " text \"" + button.Text + "\"");

            Walk(child, issues);
        }
    }

    private static bool IsChrome(Control child) =>
        child is Button or TextBox or ComboBox or DateTimePicker or CheckBox or DirectionButton;

    private static bool IsClipped(Control parent, Control child)
    {
        if (child.Dock == DockStyle.Fill)
            return false;

        var area = parent.ClientRectangle;
        if (parent is ScrollableControl { AutoScroll: true } scroll)
        {
            area = new Rectangle(
                0,
                0,
                Math.Max(scroll.ClientSize.Width, scroll.DisplayRectangle.Width),
                Math.Max(scroll.ClientSize.Height, scroll.DisplayRectangle.Height));
        }

        var rect = child.Bounds;
        return rect.Left < -2 || rect.Top < -2 || rect.Right > area.Right + 2 || rect.Bottom > area.Bottom + 2;
    }

    private static bool ButtonTextClipped(Button button)
    {
        if (string.IsNullOrEmpty(button.Text) || button.AutoEllipsis)
            return false;

        var size = TextRenderer.MeasureText(button.Text, button.Font);
        var width = button.ClientSize.Width - button.Padding.Horizontal - 8;
        var height = button.ClientSize.Height - button.Padding.Vertical - 4;
        return size.Width > width || size.Height > height;
    }

    private static string Describe(Control parent, Control child)
    {
        var name = string.IsNullOrEmpty(child.Name) ? child.GetType().Name : child.Name;
        var text = child is Button button ? button.Text : child.AccessibleName ?? "";
        return parent.GetType().Name + " clips " + name + " " + text + " at " + child.Bounds + " inside " + parent.ClientSize;
    }
}
