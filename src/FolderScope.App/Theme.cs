namespace FolderScope;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(244, 246, 248);
    public static readonly Color Surface = Color.White;
    public static readonly Color Ink = Color.FromArgb(24, 32, 38);
    public static readonly Color Muted = Color.FromArgb(90, 104, 116);
    public static readonly Color Line = Color.FromArgb(220, 226, 232);
    public static readonly Color Accent = Color.FromArgb(15, 110, 86);
    public static readonly Color AccentDark = Color.FromArgb(10, 78, 61);
    public static readonly Color AccentHover = Color.FromArgb(18, 130, 102);
    public static readonly Color Selected = Color.FromArgb(226, 242, 236);
    public static readonly Color Hover = Color.FromArgb(243, 247, 245);
    public static readonly Color RowAlt = Color.FromArgb(248, 250, 251);
    public static readonly Color HeaderBack = Color.FromArgb(241, 244, 247);
    public static readonly Color HeaderText = Color.FromArgb(60, 72, 82);
    public static readonly Color BarTrack = Color.FromArgb(232, 236, 240);
    public static readonly Color BarFill = Color.FromArgb(15, 110, 86);
    public static readonly Color FileBar = Color.FromArgb(120, 144, 156);
    public static readonly Color Danger = Color.FromArgb(154, 52, 18);
    public static readonly Color WarningBg = Color.FromArgb(255, 247, 237);
    public static readonly Color WarningText = Color.FromArgb(120, 53, 15);
    public static readonly Color Top = Color.FromArgb(18, 48, 42);
    public static readonly Color TopText = Color.FromArgb(236, 244, 240);
    public static readonly Color TopMuted = Color.FromArgb(176, 204, 194);
    public static readonly Color Disabled = Color.FromArgb(232, 236, 240);
    public static readonly Color Other = Color.FromArgb(176, 188, 196);
    public static readonly Color FolderGlyph = Color.FromArgb(15, 110, 86);
    public static readonly Color FileGlyph = Color.FromArgb(96, 116, 128);

    public static readonly Color[] Segments =
    [
        Color.FromArgb(15, 110, 86),
        Color.FromArgb(37, 99, 235),
        Color.FromArgb(180, 83, 9),
        Color.FromArgb(109, 40, 217),
        Color.FromArgb(190, 18, 60),
        Color.FromArgb(14, 116, 144),
        Color.FromArgb(161, 98, 7)
    ];

    public static Font Create(string family, float size, FontStyle style)
    {
        try
        {
            return new Font(family, size, style, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }
    }
}
