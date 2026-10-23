using System.Runtime.InteropServices;

namespace FolderScope;

internal sealed class MainForm : Form
{
    private readonly string[] _args;
    private readonly bool _demo;
    private readonly bool _remember;
    private readonly string? _shotPath;
    private readonly BrowserPane _left = new();
    private readonly BrowserPane _right = new();
    private readonly TableLayoutPanel _shell = new();
    private readonly CommandStrip _strip = new();
    private readonly DirectionButton _copyRight;
    private readonly DirectionButton _moveRight;
    private readonly DirectionButton _copyLeft;
    private readonly DirectionButton _moveLeft;
    private readonly Button _dualButton = new();
    private readonly ToolTip _tips = new() { InitialDelay = 350, ReshowDelay = 120, AutoPopDelay = 8000 };
    private BrowserPane _active;
    private bool _dual;
    private bool _shown;
    private bool _busy;
    private LocalClipboard? _local;

    public MainForm(string[] args, bool demo = false, string? shotPath = null, Size? clientSize = null)
    {
        _args = args;
        _demo = demo;
        _remember = !demo;
        _shotPath = shotPath;
        _active = _left;
        FormScale.Apply(this);
        Text = "FolderScope";
        Font = Theme.Create("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Theme.Window;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = clientSize is { Width: >= 640, Height: >= 480 } size ? size : new Size(1180, 760);
        MinimumSize = new Size(880, 620);
        KeyPreview = true;
        var icon = BrowserPane.LoadWindowIcon();
        if (icon != null)
            Icon = icon;

        _copyRight = Direction("Copy selected to right pane (F5)", DirectionAction.CopyToRight);
        _moveRight = Direction("Move selected to right pane (F6)", DirectionAction.MoveToRight);
        _copyLeft = Direction("Copy selected to left pane (F5)", DirectionAction.CopyToLeft);
        _moveLeft = Direction("Move selected to left pane (F6)", DirectionAction.MoveToLeft);
        _copyRight.Click += (_, _) => TransferBetween(_left, _right, TransferKind.Copy);
        _moveRight.Click += (_, _) => TransferBetween(_left, _right, TransferKind.Move);
        _copyLeft.Click += (_, _) => TransferBetween(_right, _left, TransferKind.Copy);
        _moveLeft.Click += (_, _) => TransferBetween(_right, _left, TransferKind.Move);
        _strip.Flow.Controls.AddRange([_copyRight, _moveRight, _copyLeft, _moveLeft]);

        var header = BuildHeader();
        var commands = BuildCommands();
        _shell.Dock = DockStyle.Fill;
        _shell.ColumnCount = 3;
        _shell.RowCount = 1;
        _shell.BackColor = Theme.Line;
        _shell.Padding = Padding.Empty;
        _shell.Margin = Padding.Empty;
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _left.Dock = DockStyle.Fill;
        _right.Dock = DockStyle.Fill;
        _strip.Dock = DockStyle.Fill;
        _left.Margin = new Padding(0, 0, 1, 0);
        _right.Margin = new Padding(1, 0, 0, 0);
        _strip.Margin = Padding.Empty;
        _shell.Controls.Add(_left, 0, 0);
        _shell.Controls.Add(_strip, 1, 0);
        _shell.Controls.Add(_right, 2, 0);

        Controls.Add(_shell);
        Controls.Add(commands);
        Controls.Add(header);

        _left.Activated += (_, _) => SetActive(_left);
        _right.Activated += (_, _) => SetActive(_right);
        _left.CommandRequested += (_, command) => RunCommand(_left, command);
        _right.CommandRequested += (_, command) => RunCommand(_right, command);
        _left.DropRequested += (_, request) => BeginInvoke(() => OnDrop(request));
        _right.DropRequested += (_, request) => BeginInvoke(() => OnDrop(request));
        _left.PathChanged += (_, _) => RefreshChrome();
        _right.PathChanged += (_, _) => RefreshChrome();
        _left.StateChanged += (_, _) => RefreshChrome();
        _right.StateChanged += (_, _) => RefreshChrome();
        _left.SelectionChanged += (_, _) => UpdateStrip();
        _right.SelectionChanged += (_, _) => UpdateStrip();

        SetActive(_left);
        SetDual(_demo || PanePreference.LoadDual());
        UpdateStrip();
    }

    private DirectionButton Direction(string caption, DirectionAction action)
    {
        var button = new DirectionButton(action, caption) { Name = action.ToString() };
        _tips.SetToolTip(button, caption);
        return button;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_shown)
            return;

        _shown = true;
        if (_demo)
        {
            var (left, right) = DemoData.Create();
            _left.OpenPath(left);
            _right.OpenPath(right);
            if (!string.IsNullOrEmpty(_shotPath))
                BeginCapture(_shotPath);
            else
                BeginRevealDemo();
            return;
        }

        if (_args.Length > 0 && !_args[0].StartsWith('-'))
            _left.OpenPath(_args[0]);
    }

    private async void BeginRevealDemo()
    {
        if (!await WaitUntilListed())
            return;

        _left.SelectFirst();
        _right.SelectFirst();
        UpdateStrip();
        try
        {
            var ready = Path.Combine(Path.GetTempPath(), "FolderScope-demo", "ready.txt");
            File.WriteAllText(ready, "ok");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task<bool> WaitUntilListed()
    {
        for (var i = 0; i < 300; i++)
        {
            await Task.Delay(100);
            if (IsDisposed)
                return false;
            if (!_left.IsScanning && !_right.IsScanning && _left.RootNode != null && _right.RootNode != null
                && _left.VisibleRowCount > 0 && _right.VisibleRowCount > 0)
                return true;
        }

        return !IsDisposed;
    }

    private async void BeginCapture(string path)
    {
        try
        {
            await WaitUntilListed();
            _left.SelectFirst();
            _right.SelectFirst();
            await Task.Delay(250);
            PerformLayout();
            using var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
            DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            var issues = LayoutAudit.Collect(this);
            File.WriteAllLines(path + ".layout.txt", issues.Count == 0 ? ["ok"] : issues);
            Close();
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(path + ".layout.txt", ex.ToString());
            }
            catch
            {
            }

            Close();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _left.Stop();
        _right.Stop();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Typing())
        {
            if (keyData == Keys.Escape)
            {
                _active.HandleKey(keyData);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        switch (keyData)
        {
            case Keys.Control | Keys.C:
                CopySelection(cut: false);
                return true;
            case Keys.Control | Keys.X:
                CopySelection(cut: true);
                return true;
            case Keys.Control | Keys.V:
                Paste(_active.CurrentPath);
                return true;
            case Keys.F5:
                CopyToOtherOrRefresh();
                return true;
            case Keys.F6:
                MoveToOther();
                return true;
            case Keys.F2:
                Rename();
                return true;
            case Keys.Delete:
                DeleteSelection(recycle: true);
                return true;
            case Keys.Shift | Keys.Delete:
                DeleteSelection(recycle: false);
                return true;
            case Keys.Control | Keys.R:
                _active.RefreshFolder();
                return true;
            case Keys.Control | Keys.Tab:
            case Keys.Control | Keys.Shift | Keys.Tab:
                if (_dual)
                    SetActive(_active == _left ? _right : _left);
                return true;
        }

        if (_active.HandleKey(keyData))
            return true;

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Top,
            Padding = new Padding(16, 8, 8, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var about = HeaderButton("About");
        about.Click += (_, _) =>
        {
            using var dialog = new AboutForm();
            dialog.ShowDialog(this);
        };
        _dualButton.Text = "Dual pane";
        StyleHeaderButton(_dualButton);
        _dualButton.Click += (_, _) => SetDual(!_dual);

        var title = new Label
        {
            Text = "FolderScope",
            AutoSize = true,
            Margin = new Padding(0, 4, 16, 0),
            ForeColor = Theme.TopText,
            BackColor = Theme.Top,
            Font = Theme.Create("Segoe UI", 14f, FontStyle.Bold)
        };
        var subtitle = new Label
        {
            Text = "Folder sizes, side by side",
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.TopMuted,
            BackColor = Theme.Top
        };
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Theme.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        actions.Controls.Add(_dualButton);
        actions.Controls.Add(about);
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 1, 0);
        header.Controls.Add(actions, 2, 0);
        return header;
    }

    private Control BuildCommands()
    {
        var bar = new WrapFlow
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 6, 8, 6),
            BackColor = Theme.Surface
        };
        bar.Controls.Add(Command("Cut", () => CopySelection(true)));
        bar.Controls.Add(Command("Paste", () => Paste(_active.CurrentPath)));
        bar.Controls.Add(Command("Delete", () => DeleteSelection(true)));
        bar.Controls.Add(Command("Rename", Rename));
        bar.Controls.Add(Command("New folder", NewFolder));
        return bar;
    }

    private void SetDual(bool dual)
    {
        _dual = dual;
        _dualButton.Text = dual ? "Single pane" : "Dual pane";
        var dualText = TextRenderer.MeasureText(_dualButton.Text, _dualButton.Font);
        _dualButton.MinimumSize = new Size(dualText.Width + _dualButton.Padding.Horizontal + 8, dualText.Height + _dualButton.Padding.Vertical + 4);
        if (!dual && _active == _right)
            SetActive(_left);

        _strip.Visible = dual;
        _right.Visible = dual;
        _shell.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, dual ? 50 : 100);
        _shell.ColumnStyles[1] = new ColumnStyle(dual ? SizeType.AutoSize : SizeType.Absolute, dual ? 0 : 0);
        _shell.ColumnStyles[2] = new ColumnStyle(dual ? SizeType.Percent : SizeType.Absolute, dual ? 50 : 0);
        if (_remember)
            PanePreference.SaveDual(dual);
        UpdateStrip();
        PerformLayout();
    }

    private void RefreshChrome()
    {
        RefreshTitle();
        UpdateStrip();
    }

    private void UpdateStrip()
    {
        var rightReady = _dual && !string.IsNullOrEmpty(_right.CurrentPath) && _left.Selection().Length > 0;
        var leftReady = _dual && !string.IsNullOrEmpty(_left.CurrentPath) && _right.Selection().Length > 0;
        _copyRight.Enabled = rightReady;
        _moveRight.Enabled = rightReady;
        _copyLeft.Enabled = leftReady;
        _moveLeft.Enabled = leftReady;
    }

    private void TransferBetween(BrowserPane source, BrowserPane destination, TransferKind kind)
    {
        var destinationPath = destination.CurrentPath;
        var paths = source.Selection().Select(item => item.Path).ToArray();
        if (paths.Length == 0 || string.IsNullOrEmpty(destinationPath))
        {
            source.SetStatus(kind == TransferKind.Move ? "Select items to move." : "Select items to copy.");
            return;
        }

        SetActive(source);
        StartTransfer(kind, paths, destinationPath, confirmMove: kind == TransferKind.Move, clearCut: false);
    }

    private void SetActive(BrowserPane pane)
    {
        _active = pane;
        _left.Active = pane == _left;
        _right.Active = pane == _right;
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        var path = _active.CurrentPath;
        Text = string.IsNullOrEmpty(path) ? "FolderScope" : "FolderScope — " + path;
    }

    private void RunCommand(BrowserPane pane, string command)
    {
        SetActive(pane);
        switch (command)
        {
            case "copy":
                CopySelection(false);
                break;
            case "cut":
                CopySelection(true);
                break;
            case "paste":
                Paste(pane.CurrentPath);
                break;
            case "delete":
                DeleteSelection(true);
                break;
            case "rename":
                Rename();
                break;
            case "new-folder":
                NewFolder();
                break;
        }
    }

    private void CopySelection(bool cut)
    {
        var paths = SelectedPaths();
        if (paths.Length == 0)
        {
            _active.SetStatus(cut ? "Select items to cut." : "Select items to copy.");
            return;
        }

        _local = new LocalClipboard(paths, cut);
        try
        {
            ExplorerClipboard.Set(paths, cut);
            _active.SetStatus((cut ? "Cut " : "Copied ") + paths.Length + " " + Noun(paths.Length) + ". Paste into a folder.");
        }
        catch (Exception ex) when (ex is ExternalException)
        {
            _active.SetStatus((cut ? "Cut " : "Copied ") + paths.Length + " " + Noun(paths.Length) + " inside FolderScope. Explorer couldn't read the clipboard.");
        }
    }

    private void Paste(string? destination)
    {
        if (string.IsNullOrEmpty(destination))
        {
            _active.SetStatus("Open a folder to paste into.");
            return;
        }

        string[] paths;
        var cut = false;
        if (!ExplorerClipboard.TryRead(out paths, out cut))
        {
            if (_local == null)
            {
                _active.SetStatus("The clipboard has no files to paste.");
                return;
            }

            paths = _local.Paths;
            cut = _local.Cut;
        }

        StartTransfer(cut ? TransferKind.Move : TransferKind.Copy, paths, destination, confirmMove: cut, clearCut: cut);
    }

    private void CopyToOtherOrRefresh()
    {
        var other = Other(_active);
        if (_dual && !string.IsNullOrEmpty(other.CurrentPath) && SelectedPaths().Length > 0)
        {
            CopyToOther();
            return;
        }

        _active.RefreshFolder();
    }

    private void CopyToOther()
    {
        var other = Other(_active);
        if (!_dual || string.IsNullOrEmpty(other.CurrentPath))
        {
            _active.SetStatus("Open a folder in the other pane first.");
            return;
        }

        var paths = SelectedPaths();
        if (paths.Length == 0)
        {
            _active.SetStatus("Select items to copy.");
            return;
        }

        StartTransfer(TransferKind.Copy, paths, other.CurrentPath, confirmMove: false, clearCut: false);
    }

    private void MoveToOther()
    {
        var other = Other(_active);
        if (!_dual || string.IsNullOrEmpty(other.CurrentPath))
        {
            _active.SetStatus("Open a folder in the other pane first.");
            return;
        }

        var paths = SelectedPaths();
        if (paths.Length == 0)
        {
            _active.SetStatus("Select items to move.");
            return;
        }

        StartTransfer(TransferKind.Move, paths, other.CurrentPath, confirmMove: true, clearCut: false);
    }

    private void OnDrop(PaneDropRequest request)
    {
        StartTransfer(request.Move ? TransferKind.Move : TransferKind.Copy, request.Paths, request.Destination, confirmMove: request.Move, clearCut: false);
    }

    private void StartTransfer(TransferKind kind, string[] sources, string destination, bool confirmMove, bool clearCut)
    {
        if (_busy || sources.Length == 0 || string.IsNullOrWhiteSpace(destination))
            return;

        if (kind == TransferKind.Move && confirmMove && !Confirm("Move " + sources.Length + " " + Noun(sources.Length) + " to " + destination + "?", "Move"))
            return;

        var same = sources.All(source => TransferPaths.SameVolume(source, destination));
        var request = new TransferRequest
        {
            Kind = kind,
            Sources = sources,
            DestinationDirectory = destination,
            SameVolume = same
        };
        TransferResult? result = null;
        _busy = true;
        try
        {
            using var host = new OperationHost();
            host.Run(this, kind == TransferKind.Move ? "Moving" : "Copying", (callbacks, progress, token) =>
            {
                result = new FileTransfer().Execute(request, new SystemTransferIo(), callbacks, progress, token);
            });
        }
        finally
        {
            _busy = false;
        }

        if (result == null)
            return;

        Publish(result.Changes);
        if (clearCut && !result.Cancelled)
        {
            _local = null;
            try
            {
                Clipboard.Clear();
            }
            catch (ExternalException)
            {
            }
        }
        _active.SetStatus(Summarize(kind == TransferKind.Move ? "Moved" : "Copied", result));
        ShowErrors(result);
    }

    private void DeleteSelection(bool recycle)
    {
        var paths = SelectedPaths();
        if (paths.Length == 0)
        {
            _active.SetStatus("Select items to delete.");
            return;
        }

        var prompt = recycle
            ? "Move " + paths.Length + " " + Noun(paths.Length) + " to the Recycle Bin?"
            : "Permanently delete " + paths.Length + " " + Noun(paths.Length) + "? This cannot be undone.";
        if (!Confirm(prompt, recycle ? "Delete" : "Permanent delete"))
            return;

        if (recycle)
        {
            var shell = ShellDelete.TryRecycle(this, paths, out var error);
            if (shell == ShellDeleteResult.Recycled)
            {
                Publish(paths.Select(path => new TransferChange { Source = path, SourceRemoved = true }).ToArray());
                _active.SetStatus("Moved " + paths.Length + " " + Noun(paths.Length) + " to the Recycle Bin.");
                return;
            }

            if (shell == ShellDeleteResult.Cancelled)
            {
                _active.SetStatus(error ?? "Delete was cancelled.");
                RefreshTouched(paths);
                return;
            }

            if (!Confirm((error ?? "The Recycle Bin couldn't take these items.") + "\n\nDelete them permanently instead?", "Delete"))
            {
                RefreshTouched(paths);
                return;
            }
        }

        if (_busy)
            return;

        TransferResult? result = null;
        _busy = true;
        try
        {
            using var host = new OperationHost();
            host.Run(this, "Deleting", (callbacks, progress, token) =>
            {
                result = FileDelete.Execute(paths, new SystemTransferIo(), callbacks, progress, token);
            });
        }
        finally
        {
            _busy = false;
        }

        if (result == null)
            return;

        Publish(result.Changes);
        _active.SetStatus(Summarize("Deleted", result));
        ShowErrors(result);
    }

    private void Rename()
    {
        var items = _active.Selection();
        if (items.Length != 1)
        {
            _active.SetStatus("Select one item to rename.");
            return;
        }

        var item = items[0];
        var current = NameOf(item.Path);
        if (!NamePrompt.TryAsk(this, "Rename", "New name", current, out var name))
            return;
        if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
            return;

        var parent = ParentOf(item.Path);
        if (parent == null)
        {
            _active.SetStatus("That item can't be renamed here.");
            return;
        }

        var destination = TransferPaths.Join(parent, name);
        var io = new SystemTransferIo();
        if (io.FileExists(destination) || io.DirectoryExists(destination))
        {
            _active.SetStatus("An item named \"" + name + "\" already exists.");
            return;
        }

        try
        {
            io.MovePath(item.Path, destination, item.IsDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _active.SetStatus(FileTransfer.Describe(ex));
            return;
        }

        long bytes = 0;
        TransferTimestamps stamps = default;
        if (!item.IsDirectory && io.TryDescribe(destination, out var described, out _))
            bytes = described.Length;
        try
        {
            stamps = io.GetTimestamps(destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        Publish(
        [
            new TransferChange
            {
                Source = item.Path,
                Destination = destination,
                SourceRemoved = true,
                Created = true,
                IsDirectory = item.IsDirectory,
                Bytes = bytes,
                Timestamps = stamps
            }
        ]);
        _active.SetStatus("Renamed to " + name + ".");
    }

    private void NewFolder()
    {
        var current = _active.CurrentPath;
        if (string.IsNullOrEmpty(current))
        {
            _active.SetStatus("Open a folder first.");
            return;
        }

        var suggested = ConflictNames.NextAvailable(current, "New folder", directoryItem: true, Exists);
        if (!NamePrompt.TryAsk(this, "New folder", "Folder name", suggested, out var name))
            return;

        var path = TransferPaths.Join(current, name);
        if (Exists(path))
        {
            _active.SetStatus("An item named \"" + name + "\" already exists.");
            return;
        }

        try
        {
            new SystemTransferIo().CreateDirectory(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _active.SetStatus(FileTransfer.Describe(ex));
            return;
        }

        Publish([new TransferChange { Source = path, Destination = path, Created = true, IsDirectory = true }]);
        _active.SetStatus("Created " + name + ".");
    }

    private void Publish(IReadOnlyList<TransferChange> changes)
    {
        if (changes.Count == 0)
            return;

        var adds = changes.Where(change => change.Created).Select(WithoutRemoval).ToArray();
        var removes = changes.Where(change => change.SourceRemoved).Select(RemovalOnly).ToArray();
        var panes = new List<BrowserPane>();
        foreach (var pane in new[] { _left, _right })
        {
            if (pane.RootNode == null || !changes.Any(change => Relevant(pane, change)))
                continue;

            if (pane.IsScanning)
            {
                pane.RefreshFolder();
                continue;
            }

            panes.Add(pane);
        }

        foreach (var pane in panes)
        {
            if (adds.Length > 0)
                pane.ApplyChanges(adds, Lookup);
        }

        var unknownFolder = adds.Any(change =>
            change.IsDirectory
            && !string.IsNullOrEmpty(change.Destination)
            && !TransferPaths.SamePath(change.Source, change.Destination)
            && Lookup(change.Source) == null);
        if (unknownFolder)
        {
            foreach (var pane in panes)
                pane.RefreshFolder();
            return;
        }

        foreach (var pane in panes)
        {
            if (removes.Length > 0)
                pane.ApplyChanges(removes, Lookup);
        }
    }

    private void RefreshTouched(IReadOnlyList<string> paths)
    {
        foreach (var pane in new[] { _left, _right })
        {
            if (pane.RootNode != null && paths.Any(pane.Touches))
                pane.RefreshFolder();
        }
    }

    private FolderNode? Lookup(string path) =>
        TreeLookup.Find(_left.RootNode, path) ?? TreeLookup.Find(_right.RootNode, path);

    private string[] SelectedPaths() => _active.Selection().Select(item => item.Path).ToArray();

    private BrowserPane Other(BrowserPane pane) => pane == _left ? _right : _left;

    private bool Typing()
    {
        Control? current = this;
        while (current is ContainerControl container && container.ActiveControl != null && container.ActiveControl != current)
            current = container.ActiveControl;

        return current is TextBoxBase or ComboBox or DateTimePicker;
    }

    private bool Confirm(string text, string title) =>
        MessageBox.Show(this, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private void ShowErrors(TransferResult result)
    {
        if (result.Errors.Count == 0)
            return;

        var lines = result.Errors.Take(8).ToArray();
        var extra = result.Errors.Count > lines.Length ? "\n…" : "";
        MessageBox.Show(this, string.Join("\n", lines) + extra, "Some items were skipped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string Summarize(string verb, TransferResult result)
    {
        var done = result.Changes.Count(change => change.Destination != null || change.SourceRemoved);
        if (result.Cancelled)
            return "Cancelled after " + done + " " + Noun(done) + ".";
        if (result.Stopped)
            return "Stopped after " + done + " " + Noun(done) + ".";
        if (result.Errors.Count > 0)
            return verb + " " + done + " " + Noun(done) + ". " + result.Errors.Count + " could not be completed.";
        return verb + " " + done + " " + Noun(done) + ".";
    }

    private static bool Relevant(BrowserPane pane, TransferChange change) =>
        (!string.IsNullOrEmpty(change.Source) && pane.Touches(change.Source))
        || (!string.IsNullOrEmpty(change.Destination) && pane.Touches(change.Destination));

    private static TransferChange WithoutRemoval(TransferChange change) => new()
    {
        Source = change.Source,
        Destination = change.Destination,
        Created = true,
        IsDirectory = change.IsDirectory,
        Bytes = change.Bytes,
        Timestamps = change.Timestamps
    };

    private static TransferChange RemovalOnly(TransferChange change) => new()
    {
        Source = change.Source,
        SourceRemoved = true,
        IsDirectory = change.IsDirectory
    };

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static string Noun(int count) => count == 1 ? "item" : "items";

    private static string NameOf(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var index = trimmed.LastIndexOf(TransferPaths.Separator(trimmed));
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    private static string? ParentOf(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var separator = TransferPaths.Separator(trimmed);
        var index = trimmed.LastIndexOf(separator);
        if (index <= 0)
            return null;

        var parent = trimmed[..index];
        return parent.Length == 2 && parent[1] == ':' ? parent + separator : parent;
    }

    private static Button HeaderButton(string text)
    {
        var button = new Button { Text = text };
        StyleHeaderButton(button);
        return button;
    }

    private static void StyleHeaderButton(Button button)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.AutoEllipsis = false;
        button.FlatStyle = FlatStyle.Flat;
        button.ForeColor = Theme.TopText;
        button.BackColor = Theme.Top;
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(4, 0, 0, 0);
        button.Padding = new Padding(12, 6, 12, 6);
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 64, 56);
        var size = TextRenderer.MeasureText(button.Text, button.Font);
        button.MinimumSize = new Size(size.Width + button.Padding.Horizontal + 8, size.Height + button.Padding.Vertical + 4);
    }

    private static UiButton Command(string text, Action action)
    {
        var button = new UiButton(text, primary: false);
        button.Click += (_, _) => action();
        return button;
    }

    private sealed class LocalClipboard
    {
        public LocalClipboard(string[] paths, bool cut)
        {
            Paths = paths;
            Cut = cut;
        }

        public string[] Paths { get; }

        public bool Cut { get; }
    }
}
