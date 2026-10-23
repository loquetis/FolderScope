namespace FolderScope;

internal sealed class OverlayAction
{
    public OverlayAction(string text, bool primary, Action invoke)
    {
        Text = text;
        Primary = primary;
        Invoke = invoke;
    }

    public string Text { get; }

    public bool Primary { get; }

    public Action Invoke { get; }
}

internal sealed class MessageOverlay : Panel
{
    private readonly Panel _card;
    private readonly UsageMark _mark;
    private readonly Label _title;
    private readonly Label _body;
    private readonly FlowLayoutPanel _actions;

    public MessageOverlay()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        _card = new Panel
        {
            Width = 560,
            Height = 280,
            BackColor = Theme.Window
        };
        _mark = new UsageMark { Dock = DockStyle.Top, Height = 42 };
        _title = new Label
        {
            AutoSize = false,
            Height = 42,
            Dock = DockStyle.Top,
            ForeColor = Theme.Ink,
            Font = Theme.Create("Segoe UI", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            BackColor = Theme.Window
        };
        _body = new Label
        {
            AutoSize = false,
            Height = 96,
            Dock = DockStyle.Top,
            ForeColor = Theme.Muted,
            Padding = new Padding(0, 10, 8, 0),
            BackColor = Theme.Window
        };
        _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = Theme.Window
        };

        // Last added is docked first. Title sits under the mark, then the body, then actions.
        _card.Controls.Add(_actions);
        _card.Controls.Add(_body);
        _card.Controls.Add(_title);
        _card.Controls.Add(_mark);
        Controls.Add(_card);
        Resize += (_, _) => LayoutCard();
    }

    public void Set(string title, string body, params OverlayAction[] actions)
    {
        _title.Text = title;
        _body.Text = body;
        var existing = new Control[_actions.Controls.Count];
        _actions.Controls.CopyTo(existing, 0);
        _actions.Controls.Clear();
        foreach (var control in existing)
            control.Dispose();

        foreach (var action in actions)
        {
            var button = new UiButton(action.Text, action.Primary);
            var invoke = action.Invoke;
            button.Click += (_, _) => invoke();
            _actions.Controls.Add(button);
        }

        Visible = true;
        LayoutCard();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            BringToFront();
            LayoutCard();
        }
    }

    private void LayoutCard()
    {
        var width = Math.Max(220, Math.Min(560, Math.Max(0, ClientSize.Width - 32)));
        _card.Width = width;
        var titleSize = TextRenderer.MeasureText(_title.Text, _title.Font, new Size(width, 1000), TextFormatFlags.WordBreak);
        var bodySize = TextRenderer.MeasureText(_body.Text, _body.Font, new Size(Math.Max(40, width - 8), 4000), TextFormatFlags.WordBreak);
        _title.Height = Math.Max(36, titleSize.Height + 8);
        _body.Height = Math.Max(48, bodySize.Height + 16);
        var actionsHeight = Math.Max(48, _actions.GetPreferredSize(new Size(width, 0)).Height);
        _actions.Height = actionsHeight;
        _card.Height = _mark.Height + _title.Height + _body.Height + _actions.Height + 8;
        _card.Left = Math.Max(8, (ClientSize.Width - _card.Width) / 2);
        _card.Top = Math.Max(8, (ClientSize.Height - _card.Height) / 2 - 8);
    }
}

internal sealed class UsageMark : Control
{
    public UsageMark()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Theme.Window;
        AccessibleName = "Folder usage";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var heights = new[] { 10, 16, 12 };
        var widths = new[] { 46, 28, 38 };
        var colors = new[] { Theme.Segments[0], Theme.Segments[1], Theme.Segments[2] };
        var x = 0;
        var gap = 6;
        for (var i = 0; i < 3; i++)
        {
            var y = Height - heights[i] - 8;
            using var brush = new SolidBrush(colors[i]);
            e.Graphics.FillRectangle(brush, x, y, widths[i], heights[i]);
            x += widths[i] + gap;
        }
    }
}
