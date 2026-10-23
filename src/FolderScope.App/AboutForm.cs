namespace FolderScope;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About FolderScope";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = Theme.Create("Segoe UI", 10f, FontStyle.Regular);
        BackColor = Theme.Surface;
        FormScale.Apply(this);
        ClientSize = new Size(560, 640);
        MinimumSize = new Size(420, 360);

        var version = typeof(AboutForm).Assembly.GetName().Version;
        var versionText = version == null ? "1.0.0" : version.ToString(3);
        var body = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Theme.Surface,
            ForeColor = Theme.Ink,
            TabStop = false,
            Text =
                "FolderScope " + versionText + "\n\n" +
                "Shows which folders are using disk space. Pick a drive or a folder and the scan runs in the background. You can open a folder, sort it, and refresh it while a large drive is still being read.\n\n" +
                "Sizes are logical file sizes, where 1 KB is 1,024 bytes. They are not compressed size-on-disk. Hidden and system files are included. Shortcuts, junctions, and symbolic links are listed but not followed, so a linked folder is not counted twice.\n\n" +
                "The date filter finds files older or newer than 30 days, 90 days, 6 months, 1 year, 2 years, 5 years, or a date you choose. You can match on last modified, created, or last accessed. Last accessed is often unreliable on Windows. Match size is the size of files in range, kept next to the full total. The Files tab lists those files, and Export CSV saves them. Changing the filter uses timestamps from the scan you already ran. Each pane keeps its own filter.\n\n" +
                "The window opens in dual pane. Each side keeps its own folder, scan, and date filter. Icons between the panes copy or move the selection toward the arrow. They light up when that side has a selection and the other side has a folder. Single pane is still on the header toggle, and FolderScope remembers the choice after the first run.\n\n" +
                "F5 copies the selection to the other pane when that pane is open; otherwise it refreshes. F6 moves to the other pane. Ctrl+R refreshes. Delete sends items to the Recycle Bin. A move on the same drive is a rename; across drives it copies, checks the copy, then deletes the original. Shortcuts and junctions are not followed.\n\n" +
                "Enter — open the selected folder\n" +
                "Backspace — go up to the parent\n" +
                "F5 — copy to the other pane, or refresh\n" +
                "F6 — move to the other pane\n" +
                "F2 — rename\n" +
                "Delete — Recycle Bin\n" +
                "Ctrl+C, Ctrl+X, Ctrl+V — copy, cut, paste\n" +
                "Ctrl+Shift+C — copy the selected path\n" +
                "Esc — stop the scan, or clear the filter\n" +
                "Ctrl+F — filter names in this folder\n" +
                "Double-click a file — show it in Explorer\n\n" +
                "Folders you can't read are marked instead of failing the scan. Run as administrator when you need system folders such as System Volume Information."
        };
        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6)
        };
        Controls.Add(body);
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
    }
}
