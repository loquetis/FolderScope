using System.Globalization;
using System.Runtime.InteropServices;

namespace FolderScope;

internal static class Headless
{
    public static string LastSelfTestDetail { get; private set; } = "";

    public static int SelfTest()
    {
        var root = Path.Combine(Path.GetTempPath(), "folderscope-selftest-" + Guid.NewGuid().ToString("n"));
        try
        {
            var keep = Path.Combine(root, "keep");
            var skip = Path.Combine(root, "skip");
            Directory.CreateDirectory(keep);
            Directory.CreateDirectory(skip);
            File.WriteAllBytes(Path.Combine(keep, "data.bin"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(skip, "data.bin"), new byte[4096]);

            var links = true;
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "skip-link"), skip);
                Directory.CreateSymbolicLink(Path.Combine(keep, "loop"), keep);
                File.CreateSymbolicLink(Path.Combine(keep, "note.txt"), "data.bin");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                links = false;
            }

            // Some hosts (notably Wine) report that the link was created but never
            // surface it through directory enumeration. Only assert link behavior
            // when a direct listing can actually see the link.
            if (links)
            {
                var visible = false;
                foreach (var entry in Directory.EnumerateFileSystemEntries(root))
                {
                    if (string.Equals(Path.GetFileName(entry), "skip-link", StringComparison.OrdinalIgnoreCase))
                        visible = true;
                }

                links = visible;
            }

            var result = new FolderScanner().Scan(root);
            var rows = FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending);
            var names = string.Join(", ", rows.Select(row => row.Name + (row.IsReparsePoint ? ":link" : "") + ":" + row.SortSize));
            LastSelfTestDetail = "links=" + links
                + " size=" + result.Root.ReadSize()
                + " files=" + result.Root.ReadFiles()
                + " folders=" + result.Root.ReadFolders()
                + " error=" + (result.Root.Error ?? "")
                + " rows=[" + names + "]";

            if (result.Root.ReadSize() != 2048 + 4096)
                return 1;

            var expectedFiles = links ? 3 : 2;
            if (result.Root.ReadFiles() != expectedFiles)
                return 2;

            if (links)
            {
                var sawLink = false;
                foreach (var row in rows)
                {
                    if (row.IsReparsePoint && row.Name == "skip-link")
                        sawLink = true;
                }

                if (!sawLink)
                    return 3;
            }

            if (!string.IsNullOrEmpty(result.Root.Error))
                return 4;

            return 0;
        }
        catch (Exception ex)
        {
            LastSelfTestDetail = ex.GetType().Name + ": " + ex.Message;
            return 99;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // The temp tree is disposable.
            }
        }
    }

    public static int Scan(string path)
    {
        try
        {
            AttachParentConsole();
            var result = new FolderScanner().Scan(path);
            var root = result.Root;
            Console.WriteLine(root.FullPath);
            Console.WriteLine(
                "size=" + root.ReadSize().ToString(CultureInfo.InvariantCulture)
                + " files=" + root.ReadFiles().ToString(CultureInfo.InvariantCulture)
                + " folders=" + root.ReadFolders().ToString(CultureInfo.InvariantCulture)
                + " denied=" + root.ReadDenied().ToString(CultureInfo.InvariantCulture)
                + " cancelled=" + result.Cancelled
                + " ms=" + result.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(root.Error))
                Console.WriteLine("error=" + root.Error);

            var rows = FolderListing.Build(root, null, FolderColumn.Size, FolderSortDirection.Descending);
            var shown = 0;
            foreach (var row in rows)
            {
                if (shown == 40)
                    break;
                Console.WriteLine(
                    row.SortSize.ToString(CultureInfo.InvariantCulture).PadLeft(12)
                    + "  "
                    + row.Name);
                shown++;
            }

            return !string.IsNullOrEmpty(root.Error) && !root.HasChildren ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 99;
        }
    }

    private static void AttachParentConsole()
    {
        try
        {
            if (!AttachConsole(-1))
                AllocConsole();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch
        {
            // A missing console still lets the process return an exit code.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
