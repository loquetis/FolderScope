using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FolderScope;

internal sealed class BrowserPane : UserControl
{
    private const int CueBanner = 0x1501;

    private readonly FolderScanner _scanner = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ResultsListView _list = new();
    private readonly CompositionBar _composition = new();
    private readonly BreadcrumbBar _breadcrumb = new();
    private readonly MessageOverlay _overlay = new();
    private readonly Label _emptyLabel = new();
    private readonly StatusLine _statusLine = new();
    private readonly EllipsisLabel _statusLeft;
    private readonly EllipsisLabel _statusRight;
    private readonly TableLayoutPanel _banner = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _filterBox = new();
    private readonly UiButton _driveButton;
    private readonly UiButton _upButton;
    private readonly UiButton _refreshButton;
    private readonly UiButton _stopButton;
    private readonly UiButton _explorerButton;
    private readonly UiButton _adminButton;
    private readonly ContextMenuStrip _menu = new();
    private readonly TableLayoutPanel _layout;
    private readonly WrapFlow _toolFlow = new() { BackColor = Theme.Surface, Dock = DockStyle.Fill };
    private readonly StackedChrome _dateHost = new() { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 2), BackColor = Theme.Window };
        private readonly StackedChrome _crumbHost = new() { Dock = DockStyle.Fill, Padding = new Padding(12, 2, 12, 6), BackColor = Theme.Window };
    private readonly StackedChrome _compositionHost = new() { Dock = DockStyle.Fill, Padding = new Padding(12, 2, 12, 4), BackColor = Theme.Window };
    private readonly StackedChrome _statusHost = new() { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 8), BackColor = Theme.Surface };
    private bool _fittingRows;
    private readonly FileResultsListView _files = new();
    private readonly ComboBox _dateFieldBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 148 };
    private readonly ComboBox _ageBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 196 };
    private readonly ComboBox _customModeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly DateTimePicker _startDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 124 };
    private readonly DateTimePicker _endDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 124 };
    private readonly EllipsisLabel _dateHint = new();
    private FlowLayoutPanel _datePair = null!;
    private FlowLayoutPanel _agePair = null!;
    private FlowLayoutPanel _customPair = null!;
    private readonly Label _andLabel = new() { Text = "and", AutoSize = true, Margin = new Padding(2, 8, 8, 0), ForeColor = Theme.Muted };
    private readonly Button _foldersViewButton = MakeViewButton("Folders");
    private readonly Button _filesViewButton = MakeViewButton("Files");
    private readonly UiButton _exportButton = new("Export CSV", primary: false);
    private ToolStripMenuItem _openItem = null!;
    private ToolStripMenuItem _revealItem = null!;
    private ToolStripMenuItem _exportItem = null!;
    private bool _filesView;
    private bool _suppressDate = true;
    private bool _active;
    private long _matchBytes;
    private long _matchFiles;

    public event EventHandler? Activated;

    public event EventHandler? StateChanged;

    public event EventHandler? SelectionChanged;

    public event EventHandler? PathChanged;

    private CancellationTokenSource? _cts;
    private Task? _scanTask;
    private int _generation;
    private FolderNode? _liveRoot;
    private FolderNode? _root;
    private FolderNode? _current;
    private ScanProgress? _progress;
    private ScanResult? _lastResult;
    private string? _scanPath;
    private bool _scanning;
    private bool _admin;
    private bool _suppressFilter;
    private int _filterGeneration;
    private int _listBuild;
    private int _builtVersion = -1;
    private long _lastBuildTimestamp;
    private long _rateFiles;
    private long _rateTimestamp;
    private double _filesPerSecond;

    public BrowserPane()
    {
        _statusLeft = _statusLine.LeftText;
        _statusRight = _statusLine.RightText;
        _admin = RunningAsAdmin();
        Font = Theme.Create("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Theme.Window;
        Dock = DockStyle.Fill;

        _driveButton = new UiButton("This PC", primary: false);
        _upButton = new UiButton("Up", primary: false);
        _refreshButton = new UiButton("Refresh", primary: false);
        _stopButton = new UiButton("Stop", primary: false);
        _explorerButton = new UiButton("Show in Explorer", primary: false);
        _adminButton = new UiButton("Run as admin", primary: false);
        var browseButton = new UiButton("Browse folder", primary: true);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Theme.Window
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

        BuildTools(browseButton);
        BuildDateBar();
        BuildBreadcrumb();
        BuildComposition();
        BuildBanner();
        BuildStatus();
        _layout.Controls.Add(_toolFlow, 0, 0);
        _layout.Controls.Add(_dateHost, 0, 1);
        _layout.Controls.Add(_crumbHost, 0, 2);
        _layout.Controls.Add(_compositionHost, 0, 3);
        _layout.Controls.Add(_banner, 0, 4);
        _layout.Controls.Add(BuildContent(), 0, 5);
        _layout.Controls.Add(_statusHost, 0, 6);
        Controls.Add(_layout);
        Padding = new Padding(1);

        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += (_, _) => OnTick();

        browseButton.Click += (_, _) => BeginBrowse();
        _driveButton.Click += (_, _) => BeginChooseDrive();
        _upButton.Click += (_, _) => GoUp();
        _refreshButton.Click += (_, _) => RefreshCurrent();
        _stopButton.Click += (_, _) => StopScan();
        _explorerButton.Click += (_, _) => RevealSelection();
        _adminButton.Click += (_, _) => RunElevated();
        _list.RowActivated += (_, _) => ActivateSelection();
        _list.SortChanged += (_, _) => BeginRebuild(preserveScroll: false);
        _list.MouseUp += OnListMouseUp;
        _list.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _list.MouseDown += (_, _) => ActivatePane();
        _list.AllowDrop = true;
        _list.ItemDrag += (_, _) => BeginDrag();
        _list.DragEnter += OnDragEnter;
        _list.DragOver += OnDragOver;
        _list.DragDrop += OnDragDrop;
        _files.RowActivated += (_, _) => RevealSelection();
        _files.SortChanged += (_, _) => BeginRebuild(preserveScroll: false);
        _files.MouseUp += OnFileMouseUp;
        _files.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _files.MouseDown += (_, _) => ActivatePane();
        _files.AllowDrop = true;
        _files.ItemDrag += (_, _) => BeginDrag();
        _files.DragEnter += OnDragEnter;
        _files.DragOver += OnDragOver;
        _files.DragDrop += OnDragDrop;
        _foldersViewButton.Click += (_, _) => SetFilesView(false);
        _filesViewButton.Click += (_, _) => SetFilesView(true);
        _exportButton.Click += (_, _) => ExportCsv();
        _dateFieldBox.SelectedIndexChanged += (_, _) => OnDateChanged();
        _ageBox.SelectedIndexChanged += (_, _) => OnDateChanged();
        _customModeBox.SelectedIndexChanged += (_, _) => OnDateChanged();
        _startDate.ValueChanged += (_, _) => OnDateChanged();
        _endDate.ValueChanged += (_, _) => OnDateChanged();
        _composition.SegmentActivated += (_, row) =>
        {
            if (row.Folder != null)
                NavigateTo(row.Folder);
        };
        _filterBox.TextChanged += async (_, _) =>
        {
            if (_suppressFilter)
                return;
            var generation = ++_filterGeneration;
            await Task.Delay(120);
            if (generation != _filterGeneration || IsDisposed)
                return;
            BeginRebuild(preserveScroll: false);
        };
        _filterBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Down)
            {
                _list.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        BuildMenu();
        ShowWelcome();
        SetBrowsingChrome(false);
        UpdateButtons();
        _suppressDate = false;
        _statusLeft.Text = "Choose a drive or a folder to begin.";
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_layout != null && !_fittingRows && _layout.ClientSize.Width > 0)
        {
            _fittingRows = true;
            FitRows();
            _fittingRows = false;
        }

        base.OnLayout(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _timer.Stop();
        }

        base.Dispose(disposing);
    }

    public bool HandleKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (_filterBox.TextLength > 0 && ActiveControl == _filterBox)
            {
                _filterBox.Clear();
                return true;
            }

            StopScan();
            return true;
        }

        if (keyData == Keys.Back && ActiveControl is not TextBox)
            GoUp();
        else if (keyData == (Keys.Alt | Keys.Up))
            GoUp();
        else if (keyData == (Keys.Control | Keys.F))
        {
            _filterBox.Focus();
            _filterBox.SelectAll();
        }
        else if (keyData == (Keys.Control | Keys.Shift | Keys.C) && ActiveControl is not TextBox)
            CopyPath();
        else if (keyData == Keys.Enter && ActiveControl is not TextBox)
            ActivateSelection();
        else
            return false;

        return true;
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Top, Padding = new Padding(16, 0, 8, 0) };
        var about = new Button
        {
            Text = "About",
            Dock = DockStyle.Right,
            Width = 72,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.TopMuted,
            BackColor = Theme.Top,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        about.FlatAppearance.BorderSize = 0;
        about.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 64, 56);
        about.Click += (_, _) =>
        {
            using var dialog = new AboutForm();
            dialog.ShowDialog(this);
        };

        var title = new Label
        {
            Text = "FolderScope",
            Dock = DockStyle.Left,
            AutoSize = true,
            ForeColor = Theme.TopText,
            Font = Theme.Create("Segoe UI", 16f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Theme.Top,
            Padding = new Padding(0, 8, 0, 0)
        };
        var subtitle = new Label
        {
            Text = "See which folders are using space",
            Dock = DockStyle.Fill,
            ForeColor = Theme.TopMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Theme.Top,
            Padding = new Padding(14, 4, 0, 0)
        };
        header.Controls.Add(subtitle);
        header.Controls.Add(title);
        header.Controls.Add(about);
        return header;
    }

    private void BuildTools(UiButton browseButton)
    {
        _filterBox.BorderStyle = BorderStyle.FixedSingle;
        _filterBox.AccessibleName = "Filter names in this folder";
        _filterBox.MinimumSize = new Size(180, 0);
        _filterBox.Width = 220;
        _filterBox.Margin = new Padding(0, 4, 0, 2);
        _filterBox.HandleCreated += (_, _) =>
            SendMessage(_filterBox.Handle, CueBanner, (IntPtr)1, "Filter this folder");
        _toolFlow.Controls.AddRange([_driveButton, browseButton, _upButton, _refreshButton, _stopButton, _explorerButton, _adminButton, _filterBox]);
    }

    private void BuildDateBar()
    {
        _exportButton.Visible = false;
        _exportButton.Margin = new Padding(0, 2, 14, 0);

        var controls = new WrapFlow
        {
            BackColor = Theme.Window,
            Padding = new Padding(0, 2, 0, 2),
            Margin = new Padding(0)
        };

        _dateFieldBox.AccessibleName = "Date field";
        _dateFieldBox.Margin = new Padding(0, 2, 0, 0);
        _dateFieldBox.DropDownWidth = 180;
        _ageBox.AccessibleName = "Age";
        _ageBox.Margin = new Padding(0, 2, 0, 0);
        _ageBox.DropDownWidth = 220;
        _customModeBox.AccessibleName = "Custom date mode";
        _customModeBox.Margin = new Padding(0, 4, 8, 0);
        _startDate.Margin = new Padding(0, 3, 8, 0);
        _endDate.Margin = new Padding(0, 3, 0, 0);

        _suppressDate = true;
        _dateFieldBox.Items.AddRange(["Last modified", "Created", "Last accessed"]);
        _dateFieldBox.SelectedIndex = 0;
        _ageBox.Items.AddRange(
        [
            "All dates",
            "Older than 30 days",
            "Older than 90 days",
            "Older than 6 months",
            "Older than 1 year",
            "Older than 2 years",
            "Older than 5 years",
            "Custom date or range"
        ]);
        _ageBox.SelectedIndex = 0;
        _customModeBox.Items.AddRange(["Older than", "Newer than", "Between"]);
        _customModeBox.SelectedIndex = 0;
        _startDate.Value = DateTime.Today.AddYears(-1);
        _endDate.Value = DateTime.Today;

        _datePair = Pair(MakeInlineLabel("Date"), _dateFieldBox);
        _agePair = Pair(MakeInlineLabel("Age"), _ageBox);
        _customPair = PairGroup(_customModeBox, _startDate, _andLabel, _endDate);
        controls.Controls.Add(_foldersViewButton);
        controls.Controls.Add(_filesViewButton);
        controls.Controls.Add(_exportButton);
        controls.Controls.Add(_datePair);
        controls.Controls.Add(_agePair);
        controls.Controls.Add(_customPair);
        FitCombo(_dateFieldBox);
        FitCombo(_ageBox);
        FitCombo(_customModeBox);
        _customModeBox.Visible = false;
        _startDate.Visible = false;
        _customPair.Visible = false;
        FitFilterPairs();

        _dateHint.ForeColor = Theme.Muted;
        _dateHint.BackColor = Theme.Window;
        _dateHint.Margin = new Padding(2, 2, 2, 4);
        ApplyHint(DateFilter.Any());

        _andLabel.Visible = false;
        _endDate.Visible = false;

        _dateHost.Controls.Add(controls);
        _dateHost.Controls.Add(_dateHint);
        StyleViewButton(_foldersViewButton, true);
        StyleViewButton(_filesViewButton, false);
    }

    private static void FitCombo(ComboBox box)
    {
        var longest = 0;
        foreach (var item in box.Items)
            longest = Math.Max(longest, TextRenderer.MeasureText(item?.ToString() ?? "", box.Font).Width + 28);

        var selected = box.SelectedItem?.ToString() ?? box.Text;
        var closed = TextRenderer.MeasureText(selected, box.Font).Width + 36;
        closed = Math.Clamp(closed, 96, Math.Max(96, longest));
        box.Width = closed;
        box.MinimumSize = new Size(closed, 0);
        box.DropDownWidth = Math.Max(longest, closed);
    }

    private static FlowLayoutPanel Pair(Control label, Control field) => PairGroup(label, field);

    private static FlowLayoutPanel PairGroup(params Control[] children)
    {
        var host = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 10, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Window
        };
        host.Controls.AddRange(children);
        return host;
    }

    private void FitFilterPairs()
    {
        FitPair(_datePair);
        FitPair(_agePair);
        FitPair(_customPair);
    }

    private static void FitPair(FlowLayoutPanel host)
    {
        var width = 0;
        var height = 0;
        var any = false;
        foreach (Control child in host.Controls)
        {
            if (!child.Visible)
                continue;
            any = true;
            var pref = child.GetPreferredSize(Size.Empty);
            var childWidth = Math.Max(child.Width, Math.Max(pref.Width, child.MinimumSize.Width));
            var childHeight = Math.Max(child.Height, Math.Max(pref.Height, child.MinimumSize.Height));
            width += childWidth + child.Margin.Horizontal;
            height = Math.Max(height, childHeight + child.Margin.Vertical);
        }

        var size = any ? new Size(Math.Max(width, 1), Math.Max(height, 1)) : Size.Empty;
        host.MinimumSize = size;
        host.Size = size;
    }

    private static Label MakeInlineLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0),
            ForeColor = Theme.Muted,
            BackColor = Theme.Window
        };

    private static Button MakeViewButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(72, 30),
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(0, 2, 6, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private void BuildBreadcrumb()
    {
        _breadcrumb.Margin = Padding.Empty;
        _crumbHost.Controls.Add(_breadcrumb);
    }

    private void BuildComposition()
    {
        _composition.Margin = Padding.Empty;
        _compositionHost.Controls.Add(_composition);
    }

    private void BuildBanner()
    {
        _banner.AutoSize = true;
        _banner.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _banner.Dock = DockStyle.Fill;
        _banner.BackColor = Theme.WarningBg;
        _banner.Visible = false;
        _banner.Padding = new Padding(12, 4, 8, 4);
        _banner.ColumnCount = 2;
        _banner.RowCount = 1;
        _banner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _banner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _banner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new EllipsisLabel
        {
            Text = "Scan stopped before it finished. Totals are incomplete.",
            TipText = "Scan stopped before it finished. Totals are incomplete.",
            Dock = DockStyle.Fill,
            ForeColor = Theme.WarningText,
            BackColor = Theme.WarningBg,
            Margin = new Padding(0, 4, 8, 4)
        };
        label.MinimumSize = new Size(0, label.GetPreferredSize(Size.Empty).Height);
        var again = new UiButton("Scan again", primary: false);
        again.Margin = new Padding(0);
        again.Click += (_, _) => RefreshCurrent();
        _banner.Controls.Add(label, 0, 0);
        _banner.Controls.Add(again, 1, 0);
    }

    private Control BuildContent()
    {
        var pad = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(12, 0, 12, 8) };
        var frame = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Line, Padding = new Padding(1, 1, 1, 1) };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        _list.Dock = DockStyle.Fill;
        _list.AccessibleName = "Folder contents";
        _files.Dock = DockStyle.Fill;
        _files.Visible = false;
        _files.AccessibleName = "Matching files";
        _emptyLabel.Dock = DockStyle.Fill;
        _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyLabel.ForeColor = Theme.Muted;
        _emptyLabel.BackColor = Theme.Surface;
        _emptyLabel.Visible = false;
        _overlay.Visible = false;
        host.Controls.Add(_list);
        host.Controls.Add(_files);
        host.Controls.Add(_emptyLabel);
        host.Controls.Add(_overlay);
        _overlay.BringToFront();
        _emptyLabel.BringToFront();
        frame.Controls.Add(host);
        pad.Controls.Add(frame);
        return pad;
    }

    private void BuildStatus()
    {
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 1000;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Height = 8;
        _progressBar.MinimumSize = new Size(0, 8);
        _progressBar.Margin = new Padding(0, 2, 0, 2);
        _progressBar.Visible = false;

        _statusHost.Controls.Add(_statusLine);
        _statusHost.Controls.Add(_progressBar);
    }

    private void FitRows()
    {
        var width = Math.Max(1, _layout.ClientSize.Width);
        FitRow(0, _toolFlow, width);
        FitRow(1, _dateHost, width);
        FitRow(2, _crumbHost, width);
        FitRow(3, _compositionHost, width);
        FitRow(4, _banner, width);
        FitRow(6, _statusHost, width);
    }

    private void FitRow(int row, Control control, int width)
    {
        var height = control.Visible ? control.GetPreferredSize(new Size(width, 0)).Height : 0;
        var style = _layout.RowStyles[row];
        if (style.SizeType != SizeType.Absolute)
            style.SizeType = SizeType.Absolute;
        if (Math.Abs(style.Height - height) > 1)
            style.Height = height;
    }

    private void BuildMenu()
    {
        _openItem = new ToolStripMenuItem("Open", null, (_, _) => ActivateSelection());
        _revealItem = new ToolStripMenuItem("Show in Explorer", null, (_, _) => RevealSelection());
        var copyItems = new ToolStripMenuItem("Copy", null, (_, _) => RaiseCommand("copy"));
        var cutItems = new ToolStripMenuItem("Cut", null, (_, _) => RaiseCommand("cut"));
        var pasteItems = new ToolStripMenuItem("Paste", null, (_, _) => RaiseCommand("paste"));
        var deleteItems = new ToolStripMenuItem("Delete", null, (_, _) => RaiseCommand("delete"));
        var renameItem = new ToolStripMenuItem("Rename", null, (_, _) => RaiseCommand("rename"));
        var newFolder = new ToolStripMenuItem("New folder", null, (_, _) => RaiseCommand("new-folder"));
        var copy = new ToolStripMenuItem("Copy path", null, (_, _) => CopyPath());
        _exportItem = new ToolStripMenuItem("Export CSV", null, (_, _) => ExportCsv()) { Visible = false };
        var rescan = new ToolStripMenuItem("Rescan this folder", null, (_, _) => RefreshCurrent());
        var admin = new ToolStripMenuItem("Run as administrator", null, (_, _) => RunElevated()) { Enabled = !_admin };
        _menu.Items.AddRange([_openItem, _revealItem, new ToolStripSeparator(), copyItems, cutItems, pasteItems, deleteItems, renameItem, newFolder, copy, _exportItem, new ToolStripSeparator(), rescan, admin]);
    }

    private void ShowWelcome()
    {
        _overlay.Set(
            "See what's using space",
            "Choose a drive or any folder. FolderScope adds up every file underneath it, including hidden ones, and lists shortcuts without following them so linked folders are not counted twice.",
            new OverlayAction("Browse folder", true, BeginBrowse),
            new OverlayAction("Choose a drive", false, BeginChooseDrive));
    }

    private void ShowFailure(FolderNode root)
    {
        var actions = new List<OverlayAction>
        {
            new("Browse folder", true, BeginBrowse),
            new("Try again", false, RefreshCurrent)
        };
        if ((root.AccessDenied || root.ReadDenied() > 0) && !_admin)
            actions.Add(new OverlayAction("Run as administrator", false, RunElevated));

        var title = root.AccessDenied ? "Couldn't read this folder" : "Couldn't scan this path";
        _overlay.Set(title, (root.Error ?? "The folder could not be read.") + "\n\n" + root.FullPath, actions.ToArray());
    }

    private async void BeginBrowse()
    {
        try
        {
            await BrowseAsync();
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private async void BeginChooseDrive()
    {
        try
        {
            await ChooseDriveAsync();
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private void BeginScan(string path)
    {
        _ = StartScanAsync(path);
    }

    private async Task BrowseAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder to scan",
            UseDescriptionForTitle = true,
            AutoUpgradeEnabled = true
        };
        if (_current != null)
            dialog.InitialDirectory = _current.FullPath;

        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            await StartScanAsync(dialog.SelectedPath);
    }

    private async Task ChooseDriveAsync()
    {
        _driveButton.Enabled = false;
        try
        {
            var entries = await Task.Run(LoadDrives);
            if (IsDisposed)
                return;

            var menu = new ContextMenuStrip();
            if (entries.Count == 0)
                menu.Items.Add(new ToolStripMenuItem("No drives found") { Enabled = false });

            foreach (var entry in entries)
            {
                if (entry.Path == null)
                {
                    menu.Items.Add(new ToolStripMenuItem(entry.Label) { Enabled = false });
                    continue;
                }

                var path = entry.Path;
                var item = new ToolStripMenuItem(entry.Label);
                item.Click += (_, _) => BeginScan(path);
                menu.Items.Add(item);
            }

            menu.Show(_driveButton, new Point(0, _driveButton.Height));
        }
        finally
        {
            if (!IsDisposed)
                _driveButton.Enabled = true;
        }
    }

    private static List<DriveChoice> LoadDrives()
    {
        var results = new List<DriveChoice>();
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return results;
        }

        foreach (var drive in drives)
        {
            string root;
            try
            {
                root = drive.RootDirectory.FullName;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(new DriveChoice(null, drive.Name + "   Unavailable"));
                continue;
            }

            try
            {
                if (drive.DriveType == DriveType.Network)
                {
                    results.Add(new DriveChoice(root, drive.Name + "   Network"));
                    continue;
                }

                if (!drive.IsReady)
                {
                    results.Add(new DriveChoice(null, drive.Name + "   Not ready"));
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.DriveType.ToString()
                    : drive.VolumeLabel;
                var free = SizeFormatter.Format(drive.AvailableFreeSpace);
                var total = SizeFormatter.Format(drive.TotalSize);
                results.Add(new DriveChoice(root, drive.Name + "   " + label + "   " + free + " free of " + total));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(new DriveChoice(root, drive.Name + "   Unavailable"));
            }
        }

        return results;
    }

    private async Task StartScanAsync(string path)
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(path))
            return;

        var generation = Interlocked.Increment(ref _generation);
        _cts?.Cancel();
        var previous = _scanTask;
        if (previous != null)
        {
            try
            {
                await previous;
            }
            catch
            {
                // The previous scan already reported its own outcome.
            }
        }

        if (IsDisposed || generation != _generation)
            return;

        _cts = new CancellationTokenSource();
        var progress = new ScanProgress();
        _progress = progress;
        Volatile.Write(ref _liveRoot, null);
        _root = null;
        _current = null;
        _lastResult = null;
        _scanPath = path;
        _scanning = true;
        _builtVersion = -1;
        _filesPerSecond = 0;
        _rateFiles = 0;
        _rateTimestamp = Stopwatch.GetTimestamp();
        _suppressFilter = true;
        _filterBox.Text = "";
        _suppressFilter = false;
        _list.SetRows([], (string?)null, preserveScroll: false);
        _files.SetRows([], (string?)null, preserveScroll: false);
        _matchBytes = 0;
        _matchFiles = 0;
        _composition.Clear();
        SetBanner(false);
        SetBrowsingChrome(true);
        _overlay.Visible = false;
        _emptyLabel.Visible = false;
        _breadcrumb.SetPath(null, NavigateTo);
        Announce(path);
        _statusLeft.Text = "Starting scan…";
        _statusRight.Text = "";
        _progressBar.Visible = true;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 28;
        Reflow();
        UpdateButtons();
        _timer.Start();

        var token = _cts.Token;
        var task = Task.Run(() => _scanner.Scan(path, progress, token, node => Volatile.Write(ref _liveRoot, node)));
        _scanTask = task;

        try
        {
            var result = await task;
            ApplyResult(result, generation);
        }
        catch (Exception ex)
        {
            if (IsDisposed || generation != _generation)
                return;

            _scanning = false;
            _timer.Stop();
            _progressBar.Visible = false;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _statusLeft.Text = "Scan failed.";
            _statusRight.Text = "";
            UpdateButtons();
            _overlay.Set(
                "Scan failed",
                ex.Message,
                new OverlayAction("Browse folder", true, BeginBrowse),
                new OverlayAction("Try again", false, RefreshCurrent));
        }
    }

    private void ApplyResult(ScanResult result, int generation)
    {
        if (IsDisposed || generation != _generation)
            return;

        _scanning = false;
        _lastResult = result;
        _timer.Stop();
        _progressBar.Visible = false;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = 0;

        if (_root == null)
            AdoptRoot(result.Root);
        else
            BeginRebuild(preserveScroll: true);

        SetBanner(result.Cancelled);
        if (!string.IsNullOrEmpty(result.Root.Error) && !result.Root.HasChildren && ReferenceEquals(_current, result.Root))
            ShowFailure(result.Root);
        else
            _overlay.Visible = false;

        UpdateStatus();
        UpdateButtons();
        UpdateEmptyLabel();
    }

    private void OnTick()
    {
        if (IsDisposed)
            return;

        var live = Volatile.Read(ref _liveRoot);
        if (live != null && _root == null)
            AdoptRoot(live);

        UpdateStatus();
        if (_current == null)
            return;

        _list.ParentSize = Math.Max(0, _current.ReadSize());
        if (!_scanning)
        {
            if (!_filesView)
                _list.InvalidateLive();
            return;
        }

        var version = _current.ReadChildrenVersion();
        var seconds = (Stopwatch.GetTimestamp() - _lastBuildTimestamp) / (double)Stopwatch.Frequency;
        var count = _filesView ? _files.RowCount : _list.RowCount;
        var dateActive = CurrentDateFilter().IsActive;
        var interval = dateActive
            ? 1.2
            : count > 100_000 ? 2.0 : count > 20_000 ? 0.8 : 0.45;
        var liveSort = _filesView
            || _list.SortColumn is FolderColumn.Size or FolderColumn.Percent or FolderColumn.Files
                or FolderColumn.MatchSize or FolderColumn.MatchFiles;
        var due = seconds >= interval && (version != _builtVersion || liveSort);
        if (due || version != _builtVersion && seconds >= interval)
            BeginRebuild(preserveScroll: true);
        else if (!_filesView)
            _list.InvalidateLive();
    }

    private void AdoptRoot(FolderNode root)
    {
        _root = root;
        _current = root;
        _overlay.Visible = false;
        Announce(root.FullPath);
        _breadcrumb.SetPath(root, NavigateTo);
        UpdateButtons();
        BeginRebuild(preserveScroll: false);
    }

    private void NavigateTo(FolderNode node)
    {
        if (node.IsReparsePoint)
        {
            _statusLeft.Text = "Links are listed but not opened, so their contents are not counted twice.";
            return;
        }

        _current = node;
        Announce(node.FullPath);
        _builtVersion = -1;
        _breadcrumb.SetPath(node, NavigateTo);
        UpdateButtons();
        BeginRebuild(preserveScroll: false);
    }

    private void GoUp()
    {
        if (_current?.Parent is { } parent)
            NavigateTo(parent);
    }

    private void RefreshCurrent()
    {
        var path = _current?.FullPath ?? _scanPath;
        if (!string.IsNullOrWhiteSpace(path))
            BeginScan(path);
    }

    private void StopScan()
    {
        if (!_scanning || _cts == null)
            return;

        _cts.Cancel();
        _stopButton.Enabled = false;
        _statusLeft.Text = "Stopping…";
    }

    private void BeginRebuild(bool preserveScroll)
    {
        if (_filesView)
            BeginRebuildFiles(preserveScroll);
        else
            BeginRebuildFolders(preserveScroll);
    }

    private void BeginRebuildFolders(bool preserveScroll)
    {
        var folder = _current;
        if (folder == null)
        {
            _list.SetRows([], (string?)null, preserveScroll: false);
            _composition.Clear();
            _matchBytes = 0;
            _matchFiles = 0;
            UpdateEmptyLabel();
            return;
        }

        var build = ++_listBuild;
        var filter = _filterBox.Text;
        var dateFilter = CurrentDateFilter();
        var selected = _list.SelectedRows().Select(row => row.FullPath).ToArray();
        _list.ShowMatches = dateFilter.IsActive;
        ApplyHint(dateFilter);
        var column = _list.SortColumn;
        var direction = _list.SortDirection;
        var version = folder.ReadChildrenVersion();
        _lastBuildTimestamp = Stopwatch.GetTimestamp();

        Task.Run(() =>
            {
                var rows = FolderListing.Build(folder, filter, column, direction, dateFilter, out var match);
                return (rows, match);
            })
            .ContinueWith(task =>
            {
                if (IsDisposed || build != _listBuild || !ReferenceEquals(_current, folder) || _filesView)
                    return;

                if (task.IsFaulted)
                {
                    _statusLeft.Text = task.Exception?.GetBaseException().Message ?? "Couldn't refresh the list.";
                    return;
                }

                _builtVersion = version;
                _matchBytes = task.Result.match.Bytes;
                _matchFiles = task.Result.match.Files;
                _list.ParentSize = Math.Max(0, folder.ReadSize());
                _list.SetRows(task.Result.rows, selected, preserveScroll);
                _composition.SetRows(task.Result.rows, folder.ReadSize());
                UpdateEmptyLabel();
                UpdateStatus();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void BeginRebuildFiles(bool preserveScroll)
    {
        var folder = _current;
        if (folder == null)
        {
            _files.SetRows([], (string?)null, preserveScroll: false);
            _matchBytes = 0;
            _matchFiles = 0;
            UpdateEmptyLabel();
            return;
        }

        var build = ++_listBuild;
        var filter = _filterBox.Text;
        var dateFilter = CurrentDateFilter();
        _files.SetDateField(dateFilter.Field);
        ApplyHint(dateFilter);
        var column = _files.SortColumn;
        var direction = _files.SortDirection;
        var selected = _files.SelectedRows().Select(row => row.FullPath).ToArray();
        var version = folder.ReadChildrenVersion();
        _lastBuildTimestamp = Stopwatch.GetTimestamp();

        Task.Run(() => FileListing.Collect(folder, dateFilter, filter, column, direction))
            .ContinueWith(task =>
            {
                if (IsDisposed || build != _listBuild || !ReferenceEquals(_current, folder) || !_filesView)
                    return;

                if (task.IsFaulted)
                {
                    _statusLeft.Text = task.Exception?.GetBaseException().Message ?? "Couldn't list files.";
                    return;
                }

                _builtVersion = version;
                _matchBytes = task.Result.MatchBytes;
                _matchFiles = task.Result.MatchFiles;
                _files.SetRows(task.Result.Files, selected, preserveScroll);
                UpdateEmptyLabel();
                UpdateStatus();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void UpdateEmptyLabel()
    {
        var shown = _filesView ? _files.RowCount : _list.RowCount;
        if (_overlay.Visible || _current == null || shown > 0)
        {
            _emptyLabel.Visible = false;
            return;
        }

        if (_filesView)
        {
            if (!string.IsNullOrWhiteSpace(_filterBox.Text))
                _emptyLabel.Text = "No files match the name and date filters.";
            else if (CurrentDateFilter().IsActive)
                _emptyLabel.Text = _scanning && !_current.EnumerationComplete
                    ? "Reading this folder…"
                    : "No files under this folder match this date filter.";
            else if (_scanning && !_current.EnumerationComplete)
                _emptyLabel.Text = "Reading this folder…";
            else
                _emptyLabel.Text = "This folder has no files. Choose an age to search subfolders.";
        }
        else if (!string.IsNullOrWhiteSpace(_filterBox.Text))
            _emptyLabel.Text = "Nothing in this folder matches the filter.";
        else if (_scanning && !_current.EnumerationComplete)
            _emptyLabel.Text = "Reading this folder…";
        else if (_current.AccessDenied)
            _emptyLabel.Text = "Access denied.";
        else
            _emptyLabel.Text = "This folder is empty.";

        _emptyLabel.Visible = true;
        _emptyLabel.BringToFront();
    }

    private void UpdateStatus()
    {
        var progress = _progress;
        if (_scanning && progress != null)
        {
            var files = progress.FilesScanned;
            var now = Stopwatch.GetTimestamp();
            var elapsed = (now - _rateTimestamp) / (double)Stopwatch.Frequency;
            if (elapsed >= 0.4)
            {
                _filesPerSecond = (files - _rateFiles) / elapsed;
                _rateFiles = files;
                _rateTimestamp = now;
            }

            var current = progress.CurrentPath;
            _statusLeft.Text = string.IsNullOrEmpty(current) ? "Scanning…" : "Scanning  " + Shrink(current, 80);
            var rate = SizeFormatter.FormatRate(_filesPerSecond);
            var denied = _root?.ReadDenied() ?? 0;
            var scanMatch = CurrentDateFilter().IsActive
                ? "   ·   match " + SizeFormatter.Format(_matchBytes)
                : "";
            _statusRight.Text = SizeFormatter.FormatCount(files) + " files   ·   "
                + SizeFormatter.Format(progress.BytesScanned)
                + scanMatch
                + (rate.Length == 0 ? "" : "   ·   " + rate)
                + (denied > 0 ? "   ·   " + SizeFormatter.FormatCount(denied) + " inaccessible" : "");

            var estimate = EstimateDrive(_scanPath, progress.BytesScanned);
            if (estimate is double value)
            {
                if (_progressBar.Style != ProgressBarStyle.Continuous)
                    _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = (int)Math.Clamp(value * _progressBar.Maximum, 0, _progressBar.Maximum - 1);
            }
            else if (_progressBar.Style != ProgressBarStyle.Marquee)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 28;
            }

            return;
        }

        if (_current == null)
        {
            if (!_scanning)
            {
                _statusLeft.Text = "Choose a drive or a folder to begin.";
                _statusRight.Text = "";
            }

            return;
        }

        var deniedCount = _root?.ReadDenied() ?? 0;
        var elapsedText = _lastResult == null ? "" : "   ·   " + SizeFormatter.FormatDuration(_lastResult.Elapsed);
        var dateFilter = CurrentDateFilter();
        var matchText = dateFilter.IsActive
            ? "   ·   match " + SizeFormatter.Format(_matchBytes) + " · " + SizeFormatter.FormatCount(_matchFiles) + " files"
            : "";
        _statusLeft.Text = _lastResult?.Cancelled == true
            ? "Scan stopped early  ·  " + _current.FullPath
            : _current.FullPath;
        _statusRight.Text = SizeFormatter.Format(_current.ReadSize())
            + "   ·   " + SizeFormatter.FormatCount(_current.ReadFiles()) + " files"
            + "   ·   " + SizeFormatter.FormatCount(_current.ReadFolders()) + " folders"
            + matchText
            + (deniedCount > 0 ? "   ·   " + SizeFormatter.FormatCount(deniedCount) + " inaccessible" : "")
            + elapsedText;
    }

    private void UpdateButtons()
    {
        _upButton.Enabled = _current?.Parent != null;
        _refreshButton.Enabled = _current != null || !string.IsNullOrEmpty(_scanPath);
        _stopButton.Enabled = _scanning;
        _explorerButton.Text = _filesView ? "Open folder" : "Show in Explorer";
        _explorerButton.Enabled = _current != null || SelectedPath() != null;
        _exportButton.Visible = _filesView;
        _exportButton.Enabled = _filesView && _current != null;
        var denied = (_root?.ReadDenied() ?? 0) > 0 || (_current?.AccessDenied ?? false);
        _adminButton.Visible = !_admin && denied;
    }

    private void SetBanner(bool visible)
    {
        _banner.Visible = visible;
        Reflow();
    }

    private void SetBrowsingChrome(bool visible)
    {
        _dateHost.Visible = visible;
        _crumbHost.Visible = visible;
        _compositionHost.Visible = visible && !_filesView;
        Reflow();
    }

    private void Reflow()
    {
        if (IsHandleCreated)
            PerformLayout();
    }

    private void ActivateSelection()
    {
        if (!_list.TryGetSelected(out var row))
        {
            if (_current != null)
                Reveal(_current.FullPath, select: false);
            return;
        }

        if (row.Folder is { } folder)
        {
            if (folder.IsReparsePoint)
            {
                _statusLeft.Text = "Links are listed but not opened, so their contents are not counted twice.";
                return;
            }

            NavigateTo(folder);
            return;
        }

        Reveal(row.FullPath, select: true);
    }

    private void RevealSelection()
    {
        if (_filesView && _files.TryGetSelected(out var hit))
            Reveal(hit.FullPath, select: true);
        else if (!_filesView && _list.TryGetSelected(out var row))
            Reveal(row.FullPath, select: true);
        else if (_current != null)
            Reveal(_current.FullPath, select: false);
    }

    private void Reveal(string path, bool select)
    {
        try
        {
            var quoted = "\"" + path.Replace("\"", "", StringComparison.Ordinal) + "\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = select ? "/select," + quoted : quoted,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _statusLeft.Text = "Couldn't open Explorer. " + ex.Message;
        }
    }

    private void CopyPath()
    {
        var path = SelectedPath();
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            Clipboard.SetText(path);
            _statusLeft.Text = "Copied " + path;
        }
        catch (Exception ex)
        {
            _statusLeft.Text = "Couldn't copy the path. " + ex.Message;
        }
    }

    private void RunElevated()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable))
                return;

            var start = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas"
            };
            if (!string.IsNullOrEmpty(_current?.FullPath))
                start.ArgumentList.Add(_current.FullPath);
            else if (!string.IsNullOrEmpty(_scanPath))
                start.ArgumentList.Add(_scanPath);

            Process.Start(start);
            FindForm()?.Close();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _statusLeft.Text = "Administrator approval was cancelled.";
        }
        catch (Exception ex)
        {
            _statusLeft.Text = "Couldn't restart as administrator. " + ex.Message;
        }
    }

    private void OnListMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
            return;

        var hit = _list.HitTest(e.Location);
        if (hit.Item != null && !hit.Item.Selected)
        {
            _list.SelectedIndices.Clear();
            _list.SelectedIndices.Add(hit.Item.Index);
        }

        ShowContextMenu(_list, e.Location);
    }

    private void OnFileMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
            return;

        var hit = _files.HitTest(e.Location);
        if (hit.Item != null && !hit.Item.Selected)
        {
            _files.SelectedIndices.Clear();
            _files.SelectedIndices.Add(hit.Item.Index);
        }

        ShowContextMenu(_files, e.Location);
    }

    private void ShowContextMenu(Control anchor, Point location)
    {
        _openItem.Visible = !_filesView;
        _revealItem.Text = _filesView ? "Open folder in Explorer" : "Show in Explorer";
        _exportItem.Visible = _filesView;
        _menu.Show(anchor, location);
    }

    internal static Icon? LoadWindowIcon()
    {
        try
        {
            var stream = typeof(BrowserPane).Assembly.GetManifestResourceStream("FolderScope.app.ico");
            if (stream != null)
            {
                using (stream)
                    return new Icon(stream);
            }
        }
        catch
        {
            // Fall through to the associated executable icon.
        }

        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
                return Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            // The window still works without a custom icon.
        }

        return null;
    }

    private void Report(Exception ex)
    {
        if (IsDisposed)
            return;

        _statusLeft.Text = ex.Message;
        MessageBox.Show(this, ex.Message, "FolderScope", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static double? EstimateDrive(string? rootPath, long bytes)
    {
        if (string.IsNullOrEmpty(rootPath) || bytes <= 0)
            return null;

        try
        {
            var root = Path.GetPathRoot(rootPath);
            if (string.IsNullOrEmpty(root))
                return null;

            var scanRoot = PathHelper.NormalizeDirectory(rootPath);
            var driveRoot = PathHelper.NormalizeDirectory(root);
            if (!string.Equals(scanRoot, driveRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                return null;

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            if (used <= 0)
                return null;

            return (double)bytes / used;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Shrink(string value, int max)
    {
        if (value.Length <= max || max < 8)
            return value;

        var keepRight = (max - 1) / 2;
        var keepLeft = max - 1 - keepRight;
        return value[..keepLeft] + "…" + value[^keepRight..];
    }

    private static bool RunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    private void SetFilesView(bool files)
    {
        if (_filesView == files)
            return;

        _filesView = files;
        _list.Visible = !files;
        _files.Visible = files;
        if (files)
            _files.BringToFront();
        else
            _list.BringToFront();
        if (_emptyLabel.Visible)
            _emptyLabel.BringToFront();
        if (_overlay.Visible)
            _overlay.BringToFront();
        StyleViewButton(_foldersViewButton, !files);
        StyleViewButton(_filesViewButton, files);
        _exportButton.Visible = files;
        SetFilterCue();
        if (_root != null || _scanning)
            SetBrowsingChrome(true);
        UpdateButtons();
        BeginRebuild(preserveScroll: false);
    }

    private void OnDateChanged()
    {
        if (_suppressDate || IsDisposed)
            return;

        var custom = _ageBox.SelectedIndex == 7;
        var between = custom && _customModeBox.SelectedIndex == 2;
        _customModeBox.Visible = custom;
        _startDate.Visible = custom;
        _andLabel.Visible = between;
        _endDate.Visible = between;
        _customPair.Visible = custom;
        FitCombo(_dateFieldBox);
        FitCombo(_ageBox);
        FitCombo(_customModeBox);
        FitFilterPairs();
        ApplyHint(CurrentDateFilter());
        Reflow();
        BeginRebuild(preserveScroll: false);
    }

    private DateFilter CurrentDateFilter()
    {
        var field = _dateFieldBox.SelectedIndex switch
        {
            1 => DateField.Created,
            2 => DateField.Accessed,
            _ => DateField.Modified
        };
        var now = DateTime.UtcNow;
        return _ageBox.SelectedIndex switch
        {
            1 => DatePresets.OlderThanDays(field, 30, now),
            2 => DatePresets.OlderThanDays(field, 90, now),
            3 => DatePresets.OlderThanMonths(field, 6, now),
            4 => DatePresets.OlderThanYears(field, 1, now),
            5 => DatePresets.OlderThanYears(field, 2, now),
            6 => DatePresets.OlderThanYears(field, 5, now),
            7 => CurrentCustomFilter(field),
            _ => DateFilter.Any(field)
        };
    }

    private DateFilter CurrentCustomFilter(DateField field) =>
        _customModeBox.SelectedIndex switch
        {
            1 => DatePresets.NewerThanLocalDate(field, _startDate.Value),
            2 => DatePresets.BetweenLocalDates(field, _startDate.Value, _endDate.Value),
            _ => DatePresets.OlderThanLocalDate(field, _startDate.Value)
        };

    private void ApplyHint(DateFilter filter)
    {
        const string idle = "Filtering by age does not rescan.";
        const string idleTip = "Folder totals stay complete. Choose an age to count matching files without scanning again.";
        if (!filter.IsActive)
        {
            _dateHint.Text = idle;
            _dateHint.TipText = idleTip;
            return;
        }

        var field = filter.Field switch
        {
            DateField.Created => "created",
            DateField.Accessed => "last accessed",
            _ => "last modified"
        };
        var when = filter.Kind switch
        {
            DateMatchKind.NewerThan => "On or after " + DateText.FormatLocal(filter.StartUtc ?? default),
            DateMatchKind.Between => DateText.FormatLocal(filter.StartUtc ?? default) + " – " + DateText.FormatLocal((filter.EndUtc ?? default).AddTicks(-1)),
            _ => "Before " + DateText.FormatLocal(filter.EndUtc ?? default)
        };
        var line = when + ", by " + field + ".";
        var note = filter.Field == DateField.Accessed
            ? " Last accessed is often unreliable on Windows."
            : "";
        _dateHint.Text = line;
        _dateHint.TipText = line + note + " Match size counts those files and does not rescan. " + idleTip;
    }

    private void ExportCsv()
    {
        if (!_filesView || _current == null)
            return;

        var rows = _files.Rows;
        if (rows.Length == 0)
        {
            _statusLeft.Text = "Nothing to export.";
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "FolderScope-files.csv",
            Title = "Export matching files",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.FileName))
            return;

        try
        {
            FileCsv.WriteFile(dialog.FileName, rows);
            _statusLeft.Text = "Exported " + SizeFormatter.FormatCount(rows.Length) + " files to " + dialog.FileName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _statusLeft.Text = "Couldn't export the CSV. " + ex.Message;
        }
    }

    private string? SelectedPath()
    {
        if (_filesView && _files.TryGetSelected(out var hit))
            return hit.FullPath;
        if (!_filesView && _list.TryGetSelected(out var row))
            return row.FullPath;
        return _current?.FullPath;
    }

    private void SetFilterCue()
    {
        if (!_filterBox.IsHandleCreated)
            return;

        SendMessage(_filterBox.Handle, CueBanner, (IntPtr)1, _filesView ? "Filter file names" : "Filter this folder");
    }

    private static void StyleViewButton(Button button, bool selected)
    {
        button.BackColor = selected ? Theme.Accent : Theme.Surface;
        button.ForeColor = selected ? Color.White : Theme.Ink;
        button.FlatAppearance.BorderColor = selected ? Theme.Accent : Theme.Line;
        button.FlatAppearance.MouseOverBackColor = selected ? Theme.AccentHover : Theme.Hover;
        button.FlatAppearance.MouseDownBackColor = selected ? Theme.AccentDark : Theme.Selected;
    }

    public FolderNode? RootNode => _root;

    public string? CurrentPath => _current?.FullPath ?? _scanPath;

    public bool IsScanning => _scanning;

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Padding = value ? new Padding(2, 2, 2, 0) : new Padding(1);
            BackColor = value ? Theme.Accent : Theme.Line;
        }
    }

    public void OpenPath(string path) => BeginScan(path);

    public void Up() => GoUp();

    public void RefreshFolder() => RefreshCurrent();

    public void Stop() => StopScan();

    public void ShowSelection() => RevealSelection();

    public void FocusFilter()
    {
        _filterBox.Focus();
        _filterBox.SelectAll();
    }

    public void SetStatus(string text) => _statusLeft.Text = text;

    public int VisibleRowCount => _filesView ? _files.RowCount : _list.RowCount;

    public void SelectFirst()
    {
        var list = _filesView ? (ListView)_files : _list;
        if (list.VirtualListSize <= 0)
            return;

        list.SelectedIndices.Clear();
        list.SelectedIndices.Add(0);
    }

    public SelectedEntry[] Selection()
    {
        if (_filesView)
            return _files.SelectedRows().Select(row => new SelectedEntry(row.FullPath, false, row.IsReparsePoint)).ToArray();

        return _list.SelectedRows().Select(row => new SelectedEntry(row.FullPath, row.IsDirectory, row.IsReparsePoint)).ToArray();
    }

    public bool Touches(string path)
    {
        if (_root == null || string.IsNullOrWhiteSpace(path))
            return false;

        return TransferPaths.IsSameOrInside(_root.FullPath, path)
            || TransferPaths.IsSameOrInside(path, _root.FullPath);
    }

    public void ApplyChanges(IReadOnlyList<TransferChange> changes, Func<string, FolderNode?> lookup)
    {
        if (_root == null || changes.Count == 0)
            return;

        TreePatch.Apply(_root, changes, lookup);
        var node = _current;
        while (node != null && TreeLookup.Find(_root, node.FullPath) == null)
            node = node.Parent;

        if (node != null && !ReferenceEquals(node, _current))
            NavigateTo(node);
        else
            BeginRebuild(preserveScroll: true);

        UpdateStatus();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<PaneDropRequest>? DropRequested;

    public event EventHandler<string>? CommandRequested;

    private void Announce(string path)
    {
        AccessibleDescription = path;
        PathChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ActivatePane() => Activated?.Invoke(this, EventArgs.Empty);

    private void RaiseCommand(string command)
    {
        ActivatePane();
        CommandRequested?.Invoke(this, command);
    }

    private void BeginDrag()
    {
        var items = Selection();
        if (items.Length == 0)
            return;

        var paths = items.Select(item => item.Path).ToArray();
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, paths);
        data.SetData(PaneDrag.Format, paths);
        DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
    }

    private void OnDragEnter(object? sender, DragEventArgs e) => OnDragOver(sender, e);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        ActivatePane();
        var paths = ReadDropPaths(e.Data);
        if (paths.Length == 0 || CurrentPath == null)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        var copy = (e.KeyState & 8) == 8;
        var move = (e.KeyState & 4) == 4;
        if (copy)
            e.Effect = DragDropEffects.Copy;
        else if (move)
            e.Effect = DragDropEffects.Move;
        else
            e.Effect = TransferPaths.SameVolume(paths[0], CurrentPath) ? DragDropEffects.Move : DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        var paths = ReadDropPaths(e.Data);
        var destination = DropDestination(sender as ListView, e) ?? CurrentPath;
        if (paths.Length == 0 || string.IsNullOrEmpty(destination))
            return;

        var move = e.Effect == DragDropEffects.Move || ((e.KeyState & 4) == 4 && (e.KeyState & 8) == 0);
        if ((e.KeyState & 8) == 8)
            move = false;
        DropRequested?.Invoke(this, new PaneDropRequest(paths, destination, move));
    }

    private string? DropDestination(ListView? list, DragEventArgs e)
    {
        if (list == null || _filesView || list != _list || _current == null)
            return CurrentPath;

        var point = list.PointToClient(new Point(e.X, e.Y));
        var hit = list.HitTest(point);
        if (hit.Item != null
            && _list.TryGetRow(hit.Item.Index, out var row)
            && row.IsDirectory
            && !row.IsReparsePoint)
            return row.FullPath;

        return CurrentPath;
    }

    private static string[] ReadDropPaths(IDataObject? data)
    {
        if (data == null)
            return [];

        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
            return files.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();

        if (data.GetDataPresent(PaneDrag.Format) && data.GetData(PaneDrag.Format) is string[] paths)
            return paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();

        return [];
    }

    private readonly record struct DriveChoice(string? Path, string Label);
}

internal readonly record struct SelectedEntry(string Path, bool IsDirectory, bool IsReparse);

internal sealed class PaneDropRequest : EventArgs
{
    public PaneDropRequest(string[] paths, string destination, bool move)
    {
        Paths = paths;
        Destination = destination;
        Move = move;
    }

    public string[] Paths { get; }

    public string Destination { get; }

    public bool Move { get; }
}

internal static class PaneDrag
{
    public const string Format = "FolderScope.Paths";
}
