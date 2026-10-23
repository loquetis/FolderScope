namespace FolderScope.Core.Tests;

public class ScannerTests
{
    [Fact]
    public void Totals_roll_up_through_nested_folders()
    {
        var root = NewRoot();
        var docs = PathHelper.Combine(root, "docs");
        var fs = new FakeFileSystem();
        fs.AddDirectory(
            root,
            MakeDir("docs"),
            MakeFile("readme.txt", 10),
            MakeFile("hidden.sys", 5, attributes: FileAttributes.Hidden | FileAttributes.System));
        fs.AddDirectory(docs, MakeFile("chapter.bin", 100), MakeDir("empty"));
        fs.AddDirectory(PathHelper.Combine(docs, "empty"));

        var progress = new ScanProgress();
        var result = new FolderScanner(fs, degreeOfParallelism: 4).Scan(root, progress);

        Assert.Equal(115, result.Root.ReadSize());
        Assert.Equal(3, result.Root.ReadFiles());
        Assert.Equal(2, result.Root.ReadFolders());
        Assert.Equal(115, progress.BytesScanned);
        Assert.Equal(3, progress.FilesScanned);
        Assert.False(result.Cancelled);
        Assert.Null(result.Root.Error);
        Assert.True(result.Root.EnumerationComplete);

        var rows = FolderListing.Build(result.Root, null, FolderColumn.Size, FolderSortDirection.Descending);
        Assert.Equal("docs", rows[0].Name);
        Assert.Equal(100, rows[0].LiveSize);
    }

    [Fact]
    public void Batches_above_the_flush_threshold_keep_an_exact_total()
    {
        var root = NewRoot();
        var count = FolderScanner.FileBatchSize * 3 + 5;
        var entries = new FsEntry[count];
        for (var i = 0; i < count; i++)
            entries[i] = MakeFile("f" + i.ToString("0000") + ".bin", 3);

        var fs = new FakeFileSystem();
        fs.AddDirectory(root, entries);

        var result = new FolderScanner(fs, degreeOfParallelism: 2).Scan(root);

        Assert.Equal(count * 3L, result.Root.ReadSize());
        Assert.Equal(count, result.Root.ReadFiles());
        Assert.Equal(0, result.Root.ReadFolders());
    }

    [Fact]
    public void Directory_batches_are_all_scanned()
    {
        var root = NewRoot();
        var folders = new FsEntry[FolderScanner.DirectoryBatchSize + 4];
        var fs = new FakeFileSystem();
        for (var i = 0; i < folders.Length; i++)
        {
            var name = "dir" + i.ToString("00");
            folders[i] = MakeDir(name);
            fs.AddDirectory(PathHelper.Combine(root, name), MakeFile("a.bin", 5));
        }

        fs.AddDirectory(root, folders);
        var result = new FolderScanner(fs, degreeOfParallelism: 8).Scan(root);

        Assert.Equal(folders.Length * 5L, result.Root.ReadSize());
        Assert.Equal(folders.Length, result.Root.ReadFiles());
        Assert.Equal(folders.Length, result.Root.ReadFolders());
    }

    [Fact]
    public void Parallel_scan_matches_a_single_worker()
    {
        var root = NewRoot();
        var fs = WideTree(root);
        var serial = new FolderScanner(fs, degreeOfParallelism: 1).Scan(root);
        var parallel = new FolderScanner(fs, degreeOfParallelism: 8).Scan(root);

        Assert.Equal(serial.Root.ReadSize(), parallel.Root.ReadSize());
        Assert.Equal(serial.Root.ReadFiles(), parallel.Root.ReadFiles());
        Assert.Equal(serial.Root.ReadFolders(), parallel.Root.ReadFolders());
    }

    [Fact]
    public void Reparse_directories_are_not_followed()
    {
        var root = NewRoot();
        var real = PathHelper.Combine(root, "real");
        var link = PathHelper.Combine(root, "link");
        var fs = new FakeFileSystem();
        fs.AddDirectory(root, MakeDir("real"), MakeDir("link", reparse: true, link: real));
        fs.AddDirectory(real, MakeFile("data.bin", 5_000));
        fs.AddDirectory(link, MakeFile("data.bin", 5_000), MakeFile("extra.bin", 9_000));

        var result = new FolderScanner(fs, degreeOfParallelism: 4).Scan(root);

        Assert.Equal(5_000, result.Root.ReadSize());
        Assert.Equal(1, result.Root.ReadFiles());
        var rows = FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending);
        var linkRow = Assert.Single(rows, row => row.Name == "link");
        Assert.True(linkRow.IsReparsePoint);
        Assert.Equal(0, linkRow.LiveSize);
        Assert.Equal(real, linkRow.LinkTarget);
        Assert.Contains("→", linkRow.DisplayName);
    }

    [Fact]
    public void File_symlinks_do_not_add_the_target_size()
    {
        var root = NewRoot();
        var fs = new FakeFileSystem();
        fs.AddDirectory(
            root,
            MakeFile("real.bin", 1_000),
            MakeFile("alias.bin", 1_000, reparse: true, link: "real.bin"));

        var result = new FolderScanner(fs).Scan(root);
        var rows = FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending);

        Assert.Equal(1_000, result.Root.ReadSize());
        Assert.Equal(2, result.Root.ReadFiles());
        var alias = Assert.Single(rows, row => row.Name == "alias.bin");
        Assert.True(alias.IsReparsePoint);
        Assert.Equal(0, alias.LiveSize);
    }

    [Fact]
    public void Reparse_files_without_a_link_target_keep_their_logical_size()
    {
        var root = NewRoot();
        var fs = new FakeFileSystem();
        fs.AddDirectory(root, MakeFile("cloud.bin", 8_000, reparse: true));

        var result = new FolderScanner(fs).Scan(root);
        var row = Assert.Single(FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending));

        Assert.Equal(8_000, result.Root.ReadSize());
        Assert.False(row.IsReparsePoint);
    }

    [Fact]
    public void Access_denied_is_recorded_and_does_not_fail_the_scan()
    {
        var root = NewRoot();
        var secret = PathHelper.Combine(root, "secret");
        var fs = new FakeFileSystem();
        fs.AddDirectory(root, MakeDir("secret"), MakeFile("visible.bin", 40));
        fs.AddDirectory(secret, MakeFile("hidden.bin", 4_000));
        fs.Deny(secret);

        var result = new FolderScanner(fs, degreeOfParallelism: 2).Scan(root);
        var rows = FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending);
        var denied = Assert.Single(rows, row => row.Name == "secret");

        Assert.Equal(40, result.Root.ReadSize());
        Assert.Equal(1, result.Root.ReadDenied());
        Assert.True(denied.AccessDenied);
        Assert.Contains("access denied", denied.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Root.Error);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public void Missing_folder_is_an_error_result()
    {
        var root = NewRoot();
        var result = new FolderScanner(new FakeFileSystem()).Scan(root);

        Assert.Equal("Folder not found.", result.Root.Error);
        Assert.Equal(0, result.Root.ReadSize());
        Assert.True(result.Root.EnumerationComplete);
    }

    [Fact]
    public void A_file_path_is_rejected()
    {
        var root = NewRoot();
        var fs = new FakeFileSystem();
        fs.AddFile(root);

        var result = new FolderScanner(fs).Scan(root);

        Assert.Equal("That path is a file, not a folder.", result.Root.Error);
        Assert.Equal(0, result.Progress.DirectoriesScanned);
    }

    [Fact]
    public void Io_errors_are_kept_on_the_folder()
    {
        var root = NewRoot();
        var fs = new FakeFileSystem();
        fs.AddDirectory(root);
        fs.Fail(root, "The device is not ready.");

        var result = new FolderScanner(fs).Scan(root);

        Assert.Equal("The device is not ready.", result.Root.Error);
        Assert.False(result.Root.AccessDenied);
    }

    [Fact]
    public void Blank_path_does_not_throw()
    {
        var result = new FolderScanner(new FakeFileSystem()).Scan("   ");
        Assert.False(string.IsNullOrWhiteSpace(result.Root.Error));
    }

    [Fact]
    public async Task Cancellation_returns_a_partial_tree()
    {
        var root = NewRoot();
        var total = FolderScanner.FileBatchSize * 8;
        var entries = new FsEntry[total];
        for (var i = 0; i < total; i++)
            entries[i] = MakeFile("f" + i.ToString("0000") + ".bin", 1);

        using var pause = new ManualResetEventSlim(false);
        var fs = new FakeFileSystem
        {
            Pause = pause,
            PauseAfterYields = FolderScanner.FileBatchSize + 16,
            PausePath = root
        };
        fs.AddDirectory(root, entries);

        using var cts = new CancellationTokenSource();
        var progress = new ScanProgress();
        var task = Task.Run(() => new FolderScanner(fs, degreeOfParallelism: 1).Scan(root, progress, cts.Token));
        try
        {
            var started = DateTime.UtcNow;
            while (progress.FilesScanned < FolderScanner.FileBatchSize && !task.IsCompleted)
            {
                if (DateTime.UtcNow - started > TimeSpan.FromSeconds(5))
                    Assert.Fail("scan did not publish the first batch");
                await Task.Delay(10);
            }

            cts.Cancel();
            pause.Set();
            var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(task, finished);
        }
        finally
        {
            pause.Set();
        }

        var result = await task;
        Assert.True(result.Cancelled);
        Assert.True(result.Root.ReadSize() < total);
        Assert.True(result.Root.ReadSize() >= FolderScanner.FileBatchSize);
    }

    [Fact]
    public void A_second_scan_does_not_reuse_the_previous_tree()
    {
        var first = NewRoot();
        var second = NewRoot();
        var fs = new FakeFileSystem();
        fs.AddDirectory(first, MakeFile("a.bin", 10));
        fs.AddDirectory(second, MakeFile("b.bin", 3), MakeFile("c.bin", 4));
        var scanner = new FolderScanner(fs, degreeOfParallelism: 2);

        Assert.Equal(10, scanner.Scan(first).Root.ReadSize());
        var again = scanner.Scan(second);
        Assert.Equal(7, again.Root.ReadSize());
        Assert.Equal(2, again.Root.ReadFiles());
    }

    [Fact]
    public async Task Real_tree_skips_directory_and_file_links()
    {
        using var tree = new TempTree();
        var real = Path.Combine(tree.Root, "real");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "data.bin"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(tree.Root, "top.bin"), new byte[50]);
        Directory.CreateSymbolicLink(Path.Combine(tree.Root, "real-link"), real);
        File.CreateSymbolicLink(Path.Combine(tree.Root, "top-link.bin"), "top.bin");
        Directory.CreateDirectory(Path.Combine(tree.Root, "loop-host"));
        Directory.CreateSymbolicLink(Path.Combine(tree.Root, "loop-host", "cycle"), tree.Root);

        var scan = Task.Run(() => new FolderScanner().Scan(tree.Root));
        var finished = await Task.WhenAny(scan, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(scan, finished);

        var result = await scan;
        Assert.Equal(1050, result.Root.ReadSize());
        Assert.Equal(3, result.Root.ReadFiles());
        Assert.Null(result.Root.Error);

        var rows = FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending);
        Assert.Contains(rows, row => row.Name == "real-link" && row.IsReparsePoint && row.LiveSize == 0);
        Assert.Contains(rows, row => row.Name == "top-link.bin" && row.IsReparsePoint && row.LiveSize == 0);
    }

    [Fact]
    public void Real_scan_includes_hidden_names_unicode_and_long_paths()
    {
        using var tree = new TempTree();
        var folderName = "café_файл";
        var nested = Path.Combine(tree.Root, folderName);
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, ".hidden"), new byte[7]);
        File.WriteAllBytes(Path.Combine(nested, "файл.txt"), new byte[9]);

        var deep = tree.Root;
        for (var i = 0; i < 20; i++)
            deep = Path.Combine(deep, "folder_" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "_abcdefghij");
        Directory.CreateDirectory(deep);
        Assert.True(deep.Length > 260);
        File.WriteAllBytes(Path.Combine(deep, "leaf.bin"), new byte[77]);

        var result = new FolderScanner(degreeOfParallelism: 4).Scan(tree.Root);

        Assert.Equal(7 + 9 + 77, result.Root.ReadSize());
        Assert.Equal(3, result.Root.ReadFiles());
        Assert.Null(result.Root.Error);
        Assert.Equal(FileAttributes.None, SystemFileSystem.EnumerationOptions.AttributesToSkip);
    }

    [Fact]
    public void Real_access_denied_directory_is_skipped_when_the_os_enforces_it()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var tree = new TempTree();
        var secret = Path.Combine(tree.Root, "secret");
        Directory.CreateDirectory(secret);
        File.WriteAllBytes(Path.Combine(secret, "hidden.bin"), new byte[400]);
        File.WriteAllBytes(Path.Combine(tree.Root, "visible.bin"), new byte[15]);
        File.SetUnixFileMode(secret, UnixFileMode.None);
        try
        {
            var readable = true;
            try
            {
                _ = Directory.EnumerateFileSystemEntries(secret).Any();
            }
            catch (UnauthorizedAccessException)
            {
                readable = false;
            }

            if (readable)
                return;

            var result = new FolderScanner().Scan(tree.Root);
            Assert.Equal(15, result.Root.ReadSize());
            Assert.True(result.Root.ReadDenied() >= 1);
            Assert.Null(result.Root.Error);
        }
        finally
        {
            File.SetUnixFileMode(secret, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static FakeFileSystem WideTree(string root)
    {
        var fs = new FakeFileSystem();
        var top = new FsEntry[30];
        for (var i = 0; i < top.Length; i++)
        {
            var name = "n" + i.ToString("00");
            top[i] = MakeDir(name);
            var children = new FsEntry[12];
            for (var j = 0; j < children.Length; j++)
                children[j] = MakeFile("f" + j.ToString("00") + ".dat", 8);
            fs.AddDirectory(PathHelper.Combine(root, name), children);
        }

        fs.AddDirectory(root, top);
        return fs;
    }

    private static string NewRoot() =>
        PathHelper.NormalizeDirectory(Path.Combine(Path.GetTempPath(), "folderscope-fake", Guid.NewGuid().ToString("n")));

    private static FsEntry MakeDir(string name, bool reparse = false, string? link = null) =>
        new(name, true, 0, FileAttributes.Directory | (reparse ? FileAttributes.ReparsePoint : 0), link);

    private static FsEntry MakeFile(
        string name,
        long size,
        bool reparse = false,
        string? link = null,
        FileAttributes attributes = FileAttributes.Normal) =>
        new(
            name,
            false,
            size,
            attributes | (reparse ? FileAttributes.ReparsePoint : 0),
            link);
}

internal sealed class TempTree : IDisposable
{
    public TempTree()
    {
        Root = Path.Combine(Path.GetTempPath(), "folderscope-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
