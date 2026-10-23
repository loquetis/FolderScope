using System.Drawing.Drawing2D;

namespace FolderScope;

internal enum DirectionAction
{
    CopyToRight,
    MoveToRight,
    CopyToLeft,
    MoveToLeft
}

internal sealed class DirectionButton : Control
{
    private bool _hover;
    private bool _pressed;

    public DirectionButton(DirectionAction action, string accessibleName)
    {
        Action = action;
        AccessibleName = accessibleName;
        AccessibleRole = AccessibleRole.PushButton;
        Cursor = Cursors.Hand;
        TabStop = true;
        Margin = new Padding(0, 0, 0, 10);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        Fit();
    }

    public DirectionAction Action { get; }

    public override Size GetPreferredSize(Size proposedSize) => Size;

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Fit();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var fill = !Enabled ? Theme.Disabled : _pressed ? Theme.Selected : _hover ? Theme.Hover : Theme.Surface;
        using (var brush = new SolidBrush(fill))
            g.FillRectangle(brush, ClientRectangle);
        using (var pen = new Pen(Enabled ? Theme.Accent : Theme.Line, Focused ? 2f : 1.5f))
            g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

        var color = Enabled ? Theme.AccentDark : Theme.Muted;
        var inset = Math.Max(5, Width / 9);
        var box = new Rectangle(inset, inset, Math.Max(8, Width - inset * 2), Math.Max(8, Height - inset * 2));
        var toRight = Action is DirectionAction.CopyToRight or DirectionAction.MoveToRight;
        if (Action is DirectionAction.CopyToRight or DirectionAction.CopyToLeft)
            CommandGlyphs.DrawCopy(g, box, toRight, color, fill);
        else
            CommandGlyphs.DrawMove(g, box, toRight, color, fill);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        var fire = _pressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
        if (fire && Enabled)
            OnClick(EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    private void Fit()
    {
        var side = Math.Max(56, Font.Height * 3 + 12);
        var size = new Size(side, side);
        MinimumSize = size;
        Size = size;
    }
}

internal static class CommandGlyphs
{
    public static void DrawCopy(Graphics graphics, Rectangle box, bool toRight, Color color, Color paper)
    {
        var pageW = Math.Max(10, box.Width * 46 / 100);
        var pageH = Math.Max(12, box.Height * 58 / 100);
        var shift = Math.Max(4, pageW * 34 / 100);
        var clusterW = pageW + shift;
        var clusterH = pageH + shift;
        var arrowW = Math.Max(12, box.Width * 32 / 100);
        var arrowH = Math.Max(14, box.Height * 40 / 100);
        var gap = Math.Max(3, box.Width / 14);
        Place(box, clusterW, clusterH, arrowW, arrowH, gap, toRight, out var pages, out var arrow);
        var back = toRight
            ? new Rectangle(pages.X + shift, pages.Y, pageW, pageH)
            : new Rectangle(pages.X, pages.Y, pageW, pageH);
        var front = toRight
            ? new Rectangle(pages.X, pages.Y + shift, pageW, pageH)
            : new Rectangle(pages.X + shift, pages.Y + shift, pageW, pageH);
        DrawSheet(graphics, back, Mix(color, paper, 0.5f), color, paper, folded: false);
        DrawSheet(graphics, front, color, color, paper, folded: false);
        DrawArrow(graphics, arrow, toRight, color);
    }

    public static void DrawMove(Graphics graphics, Rectangle box, bool toRight, Color color, Color paper)
    {
        var pageW = Math.Max(11, box.Width * 44 / 100);
        var pageH = Math.Max(14, box.Height * 66 / 100);
        var arrowW = Math.Max(13, box.Width * 36 / 100);
        var arrowH = Math.Max(14, box.Height * 42 / 100);
        var gap = Math.Max(3, box.Width / 12);
        Place(box, pageW, pageH, arrowW, arrowH, gap, toRight, out var page, out var arrow);
        DrawSheet(graphics, page, color, color, paper, folded: true);
        DrawArrow(graphics, arrow, toRight, color);
    }

    private static void Place(
        Rectangle box,
        int clusterW,
        int clusterH,
        int arrowW,
        int arrowH,
        int gap,
        bool toRight,
        out Rectangle cluster,
        out Rectangle arrow)
    {
        var contentW = clusterW + gap + arrowW;
        var originX = box.Left + Math.Max(0, (box.Width - contentW) / 2);
        var clusterX = toRight ? originX : originX + arrowW + gap;
        var arrowX = toRight ? originX + clusterW + gap : originX;
        var clusterY = box.Top + Math.Max(0, (box.Height - clusterH) / 2);
        var arrowY = box.Top + Math.Max(0, (box.Height - arrowH) / 2);
        cluster = new Rectangle(clusterX, clusterY, clusterW, clusterH);
        arrow = new Rectangle(arrowX, arrowY, arrowW, arrowH);
    }

    private static void DrawSheet(Graphics graphics, Rectangle page, Color fill, Color edge, Color paper, bool folded)
    {
        var fold = Math.Max(4, page.Width / 3);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(edge, Math.Max(1.75f, page.Width / 9f))
        {
            LineJoin = LineJoin.Round,
            Alignment = PenAlignment.Inset
        };
        if (!folded || fold >= page.Width - 2 || fold >= page.Height - 2)
        {
            graphics.FillRectangle(brush, page);
            graphics.DrawRectangle(pen, page);
            return;
        }

        Point[] body =
        [
            new(page.Left, page.Top),
            new(page.Right - fold, page.Top),
            new(page.Right, page.Top + fold),
            new(page.Right, page.Bottom),
            new(page.Left, page.Bottom)
        ];
        graphics.FillPolygon(brush, body);
        graphics.DrawPolygon(pen, body);
        Point[] ear =
        [
            new(page.Right - fold, page.Top),
            new(page.Right - fold, page.Top + fold),
            new(page.Right, page.Top + fold)
        ];
        using var paperBrush = new SolidBrush(paper);
        graphics.FillPolygon(paperBrush, ear);
        graphics.DrawPolygon(pen, ear);
    }

    private static void DrawArrow(Graphics graphics, Rectangle box, bool toRight, Color color)
    {
        var head = Math.Max(7, box.Width * 58 / 100);
        var thick = Math.Max(4, box.Height * 34 / 100);
        var mid = box.Top + box.Height / 2;
        using var brush = new SolidBrush(color);
        if (toRight)
        {
            var shaftRight = box.Right - head + 2;
            graphics.FillRectangle(brush, box.Left, mid - thick / 2, Math.Max(1, shaftRight - box.Left), thick);
            Point[] tip =
            [
                new(box.Right - head, box.Top),
                new(box.Right - 1, mid),
                new(box.Right - head, box.Bottom - 1)
            ];
            graphics.FillPolygon(brush, tip);
            return;
        }

        var shaftLeft = box.Left + head - 2;
        graphics.FillRectangle(brush, shaftLeft, mid - thick / 2, Math.Max(1, box.Right - shaftLeft), thick);
        Point[] left =
        [
            new(box.Left + head, box.Top),
            new(box.Left + 1, mid),
            new(box.Left + head, box.Bottom - 1)
        ];
        graphics.FillPolygon(brush, left);
    }

    private static Color Mix(Color color, Color paper, float towardPaper)
    {
        var t = Math.Clamp(towardPaper, 0f, 1f);
        return Color.FromArgb(
            (int)(color.R + (paper.R - color.R) * t),
            (int)(color.G + (paper.G - color.G) * t),
            (int)(color.B + (paper.B - color.B) * t));
    }
}
