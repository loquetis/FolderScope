using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace FolderScope;

internal sealed class OperationHost : Form, ITransferCallbacks
{
    private readonly Label _summary = new();
    private readonly Label _current = new();
    private readonly ProgressBar _bar = new();
    private readonly Button _cancel = new();
    private CancellationTokenSource? _cts;
    private bool _finished;

    public OperationHost()
    {
        Text = "FolderScope";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = Theme.Create("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Theme.Surface;
        FormScale.Apply(this);
        MinimumSize = new Size(440, 180);
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _summary.AutoSize = true;
        _summary.MaximumSize = new Size(520, 0);
        _summary.Margin = new Padding(0, 0, 0, 8);
        _summary.Text = "Preparing…";
        _current.AutoSize = false;
        _current.AutoEllipsis = true;
        _current.Dock = DockStyle.Top;
        _current.Height = 40;
        _current.ForeColor = Theme.Muted;
        _current.Margin = new Padding(0, 0, 0, 12);
        _bar.Dock = DockStyle.Top;
        _bar.Height = 18;
        _bar.MinimumSize = new Size(0, 18);
        _bar.Margin = new Padding(0, 0, 0, 12);
        _bar.Minimum = 0;
        _bar.Maximum = 1000;
        _cancel.Text = "Cancel";
        _cancel.AutoSize = true;
        _cancel.Padding = new Padding(16, 4, 16, 4);
        _cancel.Anchor = AnchorStyles.Right;
        _cancel.Click += (_, _) => CancelWork();
        layout.Controls.Add(_summary, 0, 0);
        layout.Controls.Add(_current, 0, 1);
        layout.Controls.Add(_bar, 0, 2);
        layout.Controls.Add(_cancel, 0, 3);
        Controls.Add(layout);
        CancelButton = _cancel;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    public void Run(IWin32Window owner, string caption, Action<ITransferCallbacks, IProgress<TransferProgress>, CancellationToken> work)
    {
        Text = caption;
        _summary.Text = caption + "…";
        _cts = new CancellationTokenSource();
        var progress = new Progress<TransferProgress>(ShowProgress);
        Shown += (_, _) =>
        {
            Task.Run(() =>
            {
                try
                {
                    work(this, progress, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    BeginInvoke(() => MessageBox.Show(this, FileTransfer.Describe(ex), caption, MessageBoxButtons.OK, MessageBoxIcon.Warning));
                }
                finally
                {
                    Finish();
                }
            });
        };
        ShowDialog(owner);
    }

    public ConflictResolution OnConflict(string source, string destination, bool isDirectory)
    {
        if (InvokeRequired)
            return Invoke(() => OnConflict(source, destination, isDirectory));

        using var dialog = new ChoiceDialog(
            "Name already exists",
            (isDirectory ? "A folder" : "A file") + " named \"" + NameOf(destination) + "\" already exists in the destination.",
            "Overwrite",
            "Skip",
            "Rename",
            "Cancel",
            "Do this for all remaining conflicts");
        dialog.ShowDialog(this);
        var choice = dialog.Choice switch
        {
            0 => NameConflict.Overwrite,
            1 => NameConflict.Skip,
            2 => NameConflict.Rename,
            _ => NameConflict.Cancel
        };
        return new ConflictResolution(choice, dialog.ApplyToAll && choice != NameConflict.Cancel);
    }

    public LinkResolution OnReparse(string source, bool isDirectory)
    {
        if (InvokeRequired)
            return Invoke(() => OnReparse(source, isDirectory));

        using var dialog = new ChoiceDialog(
            "Shortcut or junction",
            "\"" + NameOf(source) + "\" is a shortcut, junction, or symbolic link. FolderScope will not follow it into the target.",
            "Copy the link",
            "Skip",
            null,
            null,
            "Do this for every remaining link");
        dialog.ShowDialog(this);
        var policy = dialog.Choice == 0 ? LinkPolicy.CopyLink : LinkPolicy.Skip;
        return new LinkResolution(policy, dialog.ApplyToAll);
    }

    public ErrorResolution OnError(string path, string message)
    {
        if (InvokeRequired)
            return Invoke(() => OnError(path, message));

        using var dialog = new ChoiceDialog(
            "Couldn't finish this item",
            NameOf(path) + "\n\n" + message,
            "Continue",
            "Stop",
            null,
            null,
            "Continue with the remaining items");
        dialog.ShowDialog(this);
        var cont = dialog.Choice == 0;
        return new ErrorResolution(cont, dialog.ApplyToAll && cont);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_finished)
        {
            CancelWork();
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    private void ShowProgress(TransferProgress progress)
    {
        if (IsDisposed)
            return;

        var files = SizeFormatter.FormatCount(progress.FilesDone) + " of " + SizeFormatter.FormatCount(progress.FilesTotal) + " files";
        var bytes = SizeFormatter.Format(progress.BytesDone) + " of " + SizeFormatter.Format(progress.BytesTotal);
        _summary.Text = files + "    ·    " + bytes;
        _current.Text = progress.Current;
        var total = progress.BytesTotal > 0 ? progress.BytesTotal : Math.Max(1, progress.FilesTotal);
        var done = progress.BytesTotal > 0 ? progress.BytesDone : progress.FilesDone;
        var fraction = total <= 0 ? 0 : Math.Clamp((double)done / total, 0, 1);
        _bar.Value = (int)Math.Round(fraction * 1000);
    }

    private void CancelWork()
    {
        _cancel.Enabled = false;
        _summary.Text = "Cancelling…";
        _cts?.Cancel();
    }

    private void Finish()
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            BeginInvoke(Finish);
            return;
        }

        _finished = true;
        Close();
    }

    private static string NameOf(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var index = trimmed.LastIndexOf(TransferPaths.Separator(trimmed));
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }
}

internal sealed class ChoiceDialog : Form
{
    private readonly CheckBox _all = new();

    public ChoiceDialog(string title, string message, string? first, string? second, string? third, string? fourth, string applyText)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = Theme.Create("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Theme.Surface;
        FormScale.Apply(this);
        MinimumSize = new Size(420, 160);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var body = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        _all.Text = applyText;
        _all.AutoSize = true;
        _all.Margin = new Padding(0, 0, 0, 12);
        var buttons = new WrapFlow { Padding = new Padding(0), Margin = Padding.Empty, BackColor = Theme.Surface };
        var labels = new[] { first, second, third, fourth };
        for (var i = 0; i < labels.Length; i++)
        {
            if (string.IsNullOrEmpty(labels[i]))
                continue;

            var index = i;
            var button = new Button
            {
                Text = labels[i],
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoEllipsis = false,
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(0, 0, 8, 8)
            };
            var measured = TextRenderer.MeasureText(button.Text, button.Font);
            button.MinimumSize = new Size(measured.Width + button.Padding.Horizontal + 12, measured.Height + button.Padding.Vertical + 8);
            button.Click += (_, _) =>
            {
                Choice = index;
                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(button);
        }

        layout.Controls.Add(body, 0, 0);
        layout.Controls.Add(_all, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        Choice = 1;
        CancelButton = buttons.Controls.OfType<Button>().LastOrDefault();
    }

    public int Choice { get; private set; }

    public bool ApplyToAll => _all.Checked;
}

internal static class NamePrompt
{
    public static bool TryAsk(IWin32Window owner, string title, string prompt, string initial, out string name)
    {
        name = "";
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
            Font = Theme.Create("Segoe UI", 9.5f, FontStyle.Regular),
            BackColor = Theme.Surface,
            MinimumSize = new Size(380, 160),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16)
        };
        FormScale.Apply(form);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoSize = true };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text = prompt, AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 8) };
        var box = new TextBox { Text = initial, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 12), MinimumSize = new Size(320, 0) };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(16, 4, 16, 4), Margin = new Padding(8, 0, 0, 0) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(12, 4, 12, 4) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(box, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) =>
        {
            box.SelectAll();
            box.Focus();
        };
        if (form.ShowDialog(owner) != DialogResult.OK)
            return false;

        var entered = box.Text.Trim();
        if (entered.Length == 0 || entered.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || entered is "." or "..")
        {
            MessageBox.Show(owner, "That name can't be used.", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        name = entered;
        return true;
    }
}

internal static class ExplorerClipboard
{
    public static void Set(string[] paths, bool cut)
    {
        var data = new DataObject();
        var list = new StringCollection();
        foreach (var path in paths)
            list.Add(path);
        data.SetFileDropList(list);
        var stream = new MemoryStream(BitConverter.GetBytes(cut ? 2 : 1));
        data.SetData("Preferred DropEffect", stream);
        Clipboard.SetDataObject(data, copy: true);
    }

    public static bool TryRead(out string[] paths, out bool cut)
    {
        paths = [];
        cut = false;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
                return false;
            if (data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
                return false;

            paths = files.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
            cut = ReadEffect(data) == 2;
            return paths.Length > 0;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    private static int ReadEffect(IDataObject data)
    {
        if (!data.GetDataPresent("Preferred DropEffect"))
            return 1;

        var raw = data.GetData("Preferred DropEffect");
        if (raw is byte[] bytes && bytes.Length >= 4)
            return BitConverter.ToInt32(bytes, 0);
        if (raw is not Stream stream)
            return 1;

        var buffer = new byte[4];
        return stream.Read(buffer, 0, 4) == 4 ? BitConverter.ToInt32(buffer, 0) : 1;
    }
}

internal enum ShellDeleteResult
{
    Recycled,
    Cancelled,
    Failed
}

internal static class ShellDelete
{
    private const uint Delete = 0x0003;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoConfirmation = 0x0010;
    private const ushort NoErrorUi = 0x0400;

    public static ShellDeleteResult TryRecycle(IWin32Window owner, string[] paths, out string? error)
    {
        error = null;
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || paths.Length == 0)
        {
            error = "The Recycle Bin is only available in the Windows app.";
            return ShellDeleteResult.Failed;
        }

        var payload = string.Join('\0', paths) + '\0';
        var buffer = Marshal.StringToHGlobalUni(payload);
        try
        {
            var op = new ShFileOp
            {
                hwnd = owner.Handle,
                wFunc = Delete,
                pFrom = buffer,
                pTo = IntPtr.Zero,
                fFlags = (ushort)(AllowUndo | NoConfirmation | NoErrorUi),
                fAnyOperationsAborted = 0,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = IntPtr.Zero
            };
            var code = SHFileOperationW(ref op);
            if (op.fAnyOperationsAborted != 0 || code == 1223)
            {
                error = "Delete was cancelled.";
                return ShellDeleteResult.Cancelled;
            }

            if (code == 0)
                return ShellDeleteResult.Recycled;

            error = Describe(code);
            return ShellDeleteResult.Failed;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string Describe(int code) => code switch
    {
        2 => "The item could not be found.",
        5 or 0x78 => "Access denied.",
        32 or 33 => "The file is in use.",
        0x7C => "The path is too long for the Recycle Bin.",
        _ => "The Recycle Bin returned error " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + "."
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHFileOperationW(ref ShFileOp fileOp);

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    private struct ShFileOp
    {
        [FieldOffset(0)] public IntPtr hwnd;
        [FieldOffset(8)] public uint wFunc;
        [FieldOffset(16)] public IntPtr pFrom;
        [FieldOffset(24)] public IntPtr pTo;
        [FieldOffset(32)] public ushort fFlags;
        [FieldOffset(36)] public int fAnyOperationsAborted;
        [FieldOffset(40)] public IntPtr hNameMappings;
        [FieldOffset(48)] public IntPtr lpszProgressTitle;
    }
}
