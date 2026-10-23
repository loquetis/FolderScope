namespace FolderScope;

internal sealed class UiButton : Button
{
    private readonly bool _primary;

    public UiButton(string text, bool primary)
    {
        _primary = primary;
        Text = text;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoEllipsis = false;
        TextAlign = ContentAlignment.MiddleCenter;
        UseCompatibleTextRendering = false;
        Padding = new Padding(14, 4, 14, 4);
        Margin = new Padding(0, 2, 8, 2);
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        FlatAppearance.BorderSize = primary ? 0 : 1;
        FlatAppearance.BorderColor = Theme.Line;
        ApplyColors();
        FitToText();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitToText();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        FitToText();
    }

    private void FitToText()
    {
        var size = TextRenderer.MeasureText(Text, Font);
        MinimumSize = new Size(size.Width + Padding.Horizontal + 12, size.Height + Padding.Vertical + 8);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        ApplyColors();
    }

    private void ApplyColors()
    {
        if (!Enabled)
        {
            BackColor = Theme.Disabled;
            ForeColor = Theme.Muted;
            FlatAppearance.BorderColor = Theme.Line;
            return;
        }

        if (_primary)
        {
            BackColor = Theme.Accent;
            ForeColor = Color.White;
            FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            FlatAppearance.MouseDownBackColor = Theme.AccentDark;
            return;
        }

        BackColor = Theme.Surface;
        ForeColor = Theme.Ink;
        FlatAppearance.BorderColor = Theme.Line;
        FlatAppearance.MouseOverBackColor = Theme.Hover;
        FlatAppearance.MouseDownBackColor = Theme.Selected;
    }
}
