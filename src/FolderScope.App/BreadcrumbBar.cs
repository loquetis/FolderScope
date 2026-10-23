namespace FolderScope;

internal sealed class BreadcrumbBar : FlowLayoutPanel
{
    public BreadcrumbBar()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = true;
        FlowDirection = FlowDirection.LeftToRight;
        BackColor = Theme.Window;
        Margin = new Padding(0);
        Padding = new Padding(0, 4, 0, 6);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        if (width < 1)
            width = 1;
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
            var w = Math.Max(child.Width, pref.Width) + child.Margin.Horizontal;
            var h = Math.Max(child.Height, pref.Height) + child.Margin.Vertical;
            if (x > 0 && x + w > inner)
            {
                y += row;
                x = 0;
                row = 0;
            }

            x += w;
            row = Math.Max(row, h);
        }

        return new Size(width, (any ? y + row : Font.Height + 8) + 4);
    }

    public void SetPath(FolderNode? node, Action<FolderNode> navigate)
    {
        SuspendLayout();
        var old = new Control[Controls.Count];
        Controls.CopyTo(old, 0);
        Controls.Clear();
        foreach (var control in old)
            control.Dispose();

        if (node != null)
        {
            var chain = new List<FolderNode>();
            for (var current = node; current != null; current = current.Parent)
                chain.Add(current);
            chain.Reverse();

            for (var i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                {
                    Controls.Add(new Label
                    {
                        Text = "›",
                        AutoSize = true,
                        ForeColor = Theme.Muted,
                        Margin = new Padding(2, 4, 2, 4),
                        Padding = new Padding(0, 2, 0, 2),
                        BackColor = Theme.Window
                    });
                }

                var target = chain[i];
                var isLast = i == chain.Count - 1;
                var textHeight = TextRenderer.MeasureText("Ay", Font).Height + 8;
                if (isLast)
                {
                    Controls.Add(new Label
                    {
                        Text = target.Name,
                        AutoSize = true,
                        MinimumSize = new Size(0, textHeight),
                        Margin = new Padding(1, 2, 1, 2),
                        Padding = new Padding(0, 2, 0, 4),
                        ForeColor = Theme.Ink,
                        BackColor = Theme.Window
                    });
                    continue;
                }

                var link = new LinkLabel
                {
                    Text = target.Name,
                    AutoSize = true,
                    MinimumSize = new Size(0, textHeight),
                    Margin = new Padding(1, 2, 1, 2),
                    Padding = new Padding(0, 2, 0, 2),
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    LinkColor = Theme.Accent,
                    ActiveLinkColor = Theme.AccentDark,
                    VisitedLinkColor = Theme.Accent,
                    BackColor = Theme.Window
                };
                link.LinkClicked += (_, _) => navigate(target);
                Controls.Add(link);
            }
        }

        ResumeLayout();
    }
}
