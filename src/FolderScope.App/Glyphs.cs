namespace FolderScope;

internal enum GlyphKind
{
    Folder,
    File,
    Link
}

internal static class Glyphs
{
    public static void Draw(Graphics graphics, Rectangle bounds, GlyphKind kind, Color color)
    {
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        try
        {
            if (kind == GlyphKind.Folder)
                DrawFolder(graphics, bounds, color);
            else
                DrawFile(graphics, bounds, color);

            if (kind == GlyphKind.Link)
                DrawLinkMark(graphics, bounds, color);
        }
        finally
        {
            graphics.SmoothingMode = previous;
        }
    }

    private static void DrawFolder(Graphics graphics, Rectangle bounds, Color color)
    {
        var tabHeight = Math.Max(2, bounds.Height / 5);
        var tabWidth = Math.Max(4, bounds.Width / 3);
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, bounds.X, bounds.Y + tabHeight, bounds.Width, bounds.Height - tabHeight);
        graphics.FillRectangle(brush, bounds.X, bounds.Y, tabWidth, tabHeight + 1);
    }

    private static void DrawFile(Graphics graphics, Rectangle bounds, Color color)
    {
        var fold = Math.Max(3, bounds.Width / 3);
        using var brush = new SolidBrush(color);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        Point[] corners =
        [
            new Point(bounds.Left, bounds.Top),
            new Point(bounds.Right - fold, bounds.Top),
            new Point(bounds.Right, bounds.Top + fold),
            new Point(bounds.Right, bounds.Bottom),
            new Point(bounds.Left, bounds.Bottom)
        ];
        path.AddPolygon(corners);
        graphics.FillPath(brush, path);
    }

    private static void DrawLinkMark(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, Math.Max(1f, bounds.Width / 8f));
        var x = bounds.Right - 2;
        var y = bounds.Bottom - 2;
        graphics.DrawLine(pen, x - 5, y, x, y - 5);
        graphics.DrawLine(pen, x - 2, y - 5, x, y - 5);
        graphics.DrawLine(pen, x, y - 5, x, y - 3);
    }
}
