namespace FolderScope.Core.Tests;

public class FileTransferTests
{
    [Fact]
    public void Conflict_names_keep_the_extension_and_skip_taken_numbers()
    {
        Assert.Equal("notes (2).txt", ConflictNames.Suffixed("notes.txt", 2, directory: false));
        Assert.Equal("photos (2)", ConflictNames.Suffixed("photos", 2, directory: true));
        Assert.Equal(".gitignore (2)", ConflictNames.Suffixed(".gitignore", 2, directory: false));

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\box\notes.txt",
            @"C:\box\notes (2).txt"
        };
        var name = ConflictNames.NextAvailable(@"C:\box", "notes.txt", directoryItem: false, taken.Contains);
        Assert.Equal("notes (3).txt", name);
    }

    [Fact]
    public void Volume_keys_distinguish_drives_and_shares()
    {
        Assert.Equal(@"C:\", TransferPaths.VolumeKey(@"C:\Photos"));
        Assert.Equal(@"C:\", TransferPaths.VolumeKey(@"\\?\C:\Photos"));
        Assert.True(TransferPaths.SameVolume(@"C:\a", @"c:\b"));
        Assert.False(TransferPaths.SameVolume(@"C:\a", @"D:\a"));
        Assert.Equal(@"\\server\share", TransferPaths.VolumeKey(@"\\server\share\dir\file"));
        Assert.True(TransferPaths.SameVolume(@"\\server\share\a", @"\\server\share\b"));
        Assert.False(TransferPaths.SameVolume(@"\\server\share\a", @"\\server\other\a"));
        Assert.Equal("The destination is inside the folder being copied.", FileTransfer.BlockedReason(@"C:\a", true, @"C:\a\nested"));
    }

    [Fact]
    public void Same_volume_move_renames_and_cross_volume_move_copies_then_deletes()
    {
        var disk = new MemoryIo();
        disk.AddDirectory(@"C:\src");
        disk.AddDirectory(@"C:\dst");
        disk.AddDirectory(@"D:\dst");
        disk.AddFile(@"C:\src\notes.txt", 12);

        var same = Transfer(disk, TransferKind.Move, true, @"C:\dst", @"C:\src\notes.txt");
        Assert.Equal(1, disk.Moves);
        Assert.Equal(0, disk.Copies);
        Assert.False(disk.FileExists(@"C:\src\notes.txt"));
        Assert.True(disk.FileExists(@"C:\dst\notes.txt"));
        Assert.True(same.Changes[0].SourceRemoved);

        disk.AddFile(@"C:\src\notes.txt", 12);
        var cross = Transfer(disk, TransferKind.Move, false, @"D:\dst", @"C:\src\notes.txt");
        Assert.Equal(1, disk.Moves);
        Assert.Equal(1, disk.Copies);
        Assert.Equal(1, disk.Deletes);
        Assert.False(disk.FileExists(@"C:\src\notes.txt"));
        Assert.Equal(12, disk.Length(@"D:\dst\notes.txt"));
        Assert.False(cross.Cancelled);
    }

    [Fact]
    public void Same_volume_directory_move_is_one_rename()
    {
        var disk = new MemoryIo();
        disk.AddDirectory(@"C:\src");
        disk.AddDirectory(@"C:\src\keep");
        disk.AddFile(@"C:\src\keep\a.bin", 4);
        disk.AddDirectory(@"C:\dst");

        Transfer(disk, TransferKind.Move, true, @"C:\dst", @"C:\src\keep");

        Assert.Equal(1, disk.Moves);
        Assert.Equal(0, disk.Copies);
        Assert.Equal(0, disk.Enumerations(@"C:\src\keep"));
        Assert.True(disk.FileExists(@"C:\dst\keep\a.bin"));
        Assert.False(disk.DirectoryExists(@"C:\src\keep"));
    }

    [Fact]
    public void Conflicts_can_rename_skip_all_or_overwrite()
    {
        var disk = new MemoryIo();
        disk.AddDirectory(@"C:\src");
        disk.AddDirectory(@"C:\dst");
        disk.AddFile(@"C:\src\notes.txt", 9);
        disk.AddFile(@"C:\dst\notes.txt", 1);
        disk.AddFile(@"C:\dst\notes (2).txt", 1);

        var renamed = Transfer(disk, TransferKind.Copy, true, @"C:\dst", @"C:\src\notes.txt", conflict: NameConflict.Rename);
        Assert.True(disk.FileExists(@"C:\dst\notes (3).txt"));
        Assert.Equal(9, disk.Length(@"C:\dst\notes (3).txt"));
        Assert.Equal(@"C:\dst\notes (3).txt", renamed.Changes[^1].Destination);

        disk.AddFile(@"C:\src\other.txt", 3);
        disk.AddFile(@"C:\src\third.txt", 4);
        disk.AddFile(@"C:\dst\other.txt", 1);
        disk.AddFile(@"C:\dst\third.txt", 1);
        var callbacks = new ScriptCallbacks { Conflict = NameConflict.Skip, ConflictAll = true };
        var skipped = new FileTransfer().Execute(Request(TransferKind.Copy, true, @"C:\dst", @"C:\src\other.txt", @"C:\src\third.txt"), disk, callbacks, null, CancellationToken.None);
        Assert.Equal(1, callbacks.ConflictAsks);
        Assert.DoesNotContain(skipped.Changes, change => change.Created);
        Assert.Equal(1, disk.Length(@"C:\dst\other.txt"));

        var overwrite = new ScriptCallbacks { Conflict = NameConflict.Overwrite };
        new FileTransfer().Execute(Request(TransferKind.Copy, true, @"C:\dst", @"C:\src\other.txt"), disk, overwrite, null, CancellationToken.None);
        Assert.Equal(3, disk.Length(@"C:\dst\other.txt"));
        Assert.Equal(1, disk.ReadOnlyClears);
    }

    [Fact]
    public void Links_are_not_followed()
    {
        var disk = new MemoryIo();
        disk.AddDirectory(@"C:\src");
        disk.AddDirectory(@"C:\dst");
        disk.AddDirectory(@"C:\secret");
        disk.AddFile(@"C:\secret\hidden.bin", 50);
        disk.AddLink(@"C:\src\jump", @"C:\secret", isDirectory: true);
        disk.AddChild(@"C:\src\jump", "hidden.bin", 50);

        var skipped = Transfer(disk, TransferKind.Copy, true, @"C:\dst", @"C:\src\jump", link: LinkPolicy.Skip);
        Assert.Equal(0, disk.Enumerations(@"C:\src\jump"));
        Assert.False(disk.DirectoryExists(@"C:\dst\jump"));
        Assert.False(skipped.Changes[0].Created);

        disk.Link = LinkPolicy.CopyLink;
        Transfer(disk, TransferKind.Copy, true, @"C:\dst", @"C:\src\jump", link: LinkPolicy.CopyLink);
        Assert.Equal(0, disk.Enumerations(@"C:\src\jump"));
        Assert.Equal(1, disk.LinksCopied);
        Assert.False(disk.FileExists(@"C:\dst\jump\hidden.bin"));
    }

    [Fact]
    public void Cancel_stops_before_the_next_file_and_errors_can_stop_the_run()
    {
        var disk = new MemoryIo();
        disk.AddDirectory(@"C:\src");
        disk.AddDirectory(@"C:\dst");
        disk.AddFile(@"C:\src\a.bin", 2);
        disk.AddFile(@"C:\src\b.bin", 2);
        using var cts = new CancellationTokenSource();
        disk.AfterCopy = () => cts.Cancel();
        var cancelled = new FileTransfer().Execute(Request(TransferKind.Copy, true, @"C:\dst", @"C:\src\a.bin", @"C:\src\b.bin"), disk, new ScriptCallbacks(), null, cts.Token);
        Assert.True(cancelled.Cancelled);
        Assert.Equal(1, disk.Copies);
        Assert.True(disk.FileExists(@"C:\dst\a.bin"));
        Assert.False(disk.FileExists(@"C:\dst\b.bin"));

        disk.Copies = 0;
        disk.ThrowOn = @"C:\src\a.bin";
        var callbacks = new ScriptCallbacks { Continue = false };
        var stopped = new FileTransfer().Execute(Request(TransferKind.Copy, false, @"C:\dst", @"C:\src\a.bin", @"C:\src\b.bin"), disk, callbacks, null, CancellationToken.None);
        Assert.True(stopped.Stopped);
        Assert.Equal(1, callbacks.ErrorAsks);
        Assert.Contains("in use", stopped.Errors[0], StringComparison.OrdinalIgnoreCase);
        Assert.False(disk.FileExists(@"C:\dst\b.bin"));
    }

    [Fact]
    public void Tree_patch_updates_totals_without_rebuilding_from_disk()
    {
        var rootPath = PathHelper.NormalizeDirectory(Path.Combine(Path.GetTempPath(), "folderscope-patch", Guid.NewGuid().ToString("n")));
        var sub = PathHelper.Combine(rootPath, "keep");
        var fs = new FakeFileSystem();
        fs.AddDirectory(rootPath, new FsEntry("a.bin", false, 10, FileAttributes.Normal, null), new FsEntry("keep", true, 0, FileAttributes.Directory, null));
        fs.AddDirectory(sub, new FsEntry("b.bin", false, 25, FileAttributes.Normal, null));
        var root = new FolderScanner(fs, 1).Scan(rootPath).Root;
        Assert.Equal(35, root.ReadSize());

        var removed = TreePatch.Remove(root, PathHelper.Combine(rootPath, "a.bin"));
        Assert.True(removed);
        Assert.Equal(25, root.ReadSize());
        Assert.Equal(1, root.ReadFiles());

        var copy = new TransferChange
        {
            Source = sub,
            Destination = PathHelper.Combine(rootPath, "keep-copy"),
            Created = true,
            IsDirectory = true,
            SourceRemoved = false
        };
        TreePatch.Apply(root, [copy], path => TreeLookup.Find(root, path));
        Assert.Equal(50, root.ReadSize());
        Assert.Equal(2, root.ReadFiles());
        Assert.Equal(2, root.ReadFolders());
        var clone = TreeLookup.Find(root, copy.Destination!);
        Assert.NotNull(clone);
        Assert.Equal(25, clone.ReadSize());

        var pasted = new TransferChange
        {
            Source = PathHelper.Combine(rootPath, "a.bin"),
            Destination = PathHelper.Combine(rootPath, "a-copy.bin"),
            Created = true,
            Bytes = 10
        };
        TreePatch.Apply(root, [pasted], null);
        Assert.Equal(60, root.ReadSize());
        Assert.Equal(3, root.ReadFiles());
    }

    [Fact]
    public void Delete_removes_files_without_following_links_and_can_cancel()
    {
        var disk = new MemoryIo();
        disk.AddDirectory(@"C:\src");
        disk.AddChild(@"C:\src", "a.bin", 4);
        disk.AddChild(@"C:\src", "b.bin", 6);
        disk.AddLink(@"C:\src\link", @"D:\secret", isDirectory: true);
        disk.AddDirectory(@"D:\secret");
        disk.AddChild(@"D:\secret", "hidden.bin", 9);

        var deleted = FileDelete.Execute([@"C:\src\link", @"C:\src\a.bin"], disk, new ScriptCallbacks(), null, CancellationToken.None);
        Assert.Empty(deleted.Errors);
        Assert.False(disk.DirectoryExists(@"C:\src\link"));
        Assert.False(disk.FileExists(@"C:\src\a.bin"));
        Assert.Equal(0, disk.Enumerations(@"C:\src\link"));
        Assert.True(disk.FileExists(@"D:\secret\hidden.bin"));

        using var cts = new CancellationTokenSource();
        disk.AfterDelete = () => cts.Cancel();
        var cancelled = FileDelete.Execute([@"C:\src\b.bin", @"D:\secret\hidden.bin"], disk, new ScriptCallbacks(), null, cts.Token);
        Assert.True(cancelled.Cancelled);
        Assert.False(disk.FileExists(@"C:\src\b.bin"));
        Assert.True(disk.FileExists(@"D:\secret\hidden.bin"));
    }

    [Fact]
    public void Real_copy_preserves_last_write_and_a_cancelled_copy_removes_the_partial()
    {
        var root = Path.Combine(Path.GetTempPath(), "folderscope-xfer-" + Guid.NewGuid().ToString("n"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dst);
        try
        {
            var file = Path.Combine(src, "old.bin");
            File.WriteAllBytes(file, new byte[32]);
            var written = new DateTime(2015, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(file, written);
            File.SetAttributes(file, FileAttributes.ReadOnly);
            var existing = Path.Combine(dst, "old.bin");
            File.WriteAllBytes(existing, new byte[4]);
            File.SetAttributes(existing, FileAttributes.ReadOnly);

            var result = new FileTransfer().Execute(
                new TransferRequest { Kind = TransferKind.Copy, SameVolume = true, DestinationDirectory = dst, Sources = [file] },
                new SystemTransferIo(),
                new ScriptCallbacks { Conflict = NameConflict.Overwrite },
                null,
                CancellationToken.None);
            Assert.Empty(result.Errors);
            Assert.Equal(32, new FileInfo(existing).Length);
            Assert.Equal(written, File.GetLastWriteTimeUtc(existing));

            var sourcePartial = Path.Combine(src, "partial.bin");
            var partial = Path.Combine(dst, "partial.bin");
            File.WriteAllBytes(sourcePartial, new byte[128]);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                new SystemTransferIo().CopyFile(sourcePartial, partial, _ => { }, cts.Token));
            Assert.False(File.Exists(partial));
        }
        finally
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static TransferResult Transfer(
        MemoryIo disk,
        TransferKind kind,
        bool sameVolume,
        string destination,
        string source,
        NameConflict conflict = NameConflict.Overwrite,
        LinkPolicy link = LinkPolicy.Skip) =>
        new FileTransfer().Execute(
            Request(kind, sameVolume, destination, source),
            disk,
            new ScriptCallbacks { Conflict = conflict, Link = link },
            null,
            CancellationToken.None);

    private static TransferRequest Request(TransferKind kind, bool sameVolume, string destination, params string[] sources) =>
        new()
        {
            Kind = kind,
            SameVolume = sameVolume,
            DestinationDirectory = destination,
            Sources = sources
        };

    private sealed class ScriptCallbacks : ITransferCallbacks
    {
        public NameConflict Conflict { get; set; } = NameConflict.Overwrite;

        public bool ConflictAll { get; set; }

        public int ConflictAsks { get; set; }

        public LinkPolicy Link { get; set; } = LinkPolicy.Skip;

        public bool Continue { get; set; } = true;

        public int ErrorAsks { get; set; }

        public ConflictResolution OnConflict(string source, string destination, bool isDirectory)
        {
            ConflictAsks++;
            return new ConflictResolution(Conflict, ConflictAll);
        }

        public LinkResolution OnReparse(string source, bool isDirectory) =>
            new(Link, ApplyToAll: true);

        public ErrorResolution OnError(string path, string message)
        {
            ErrorAsks++;
            return new ErrorResolution(Continue, ApplyToAll: false);
        }
    }

    private sealed class MemoryIo : ITransferIo
    {
        private readonly Dictionary<string, Node> _nodes = new(StringComparer.OrdinalIgnoreCase);

        public int Moves { get; private set; }

        public int Copies { get; set; }

        public int Deletes { get; private set; }

        public int LinksCopied { get; private set; }

        public int ReadOnlyClears { get; private set; }

        public string? ThrowOn { get; set; }

        public Action? AfterCopy { get; set; }

        public Action? AfterDelete { get; set; }

        public LinkPolicy Link { get; set; }

        public void AddDirectory(string path) => _nodes[Key(path)] = new Node { IsDirectory = true };

        public void AddFile(string path, int size) =>
            _nodes[Key(path)] = new Node { IsDirectory = false, Data = new byte[size], ReadOnly = true };

        public void AddLink(string path, string target, bool isDirectory) =>
            _nodes[Key(path)] = new Node { IsDirectory = isDirectory, IsReparse = true, LinkTarget = target };

        public void AddChild(string directory, string name, int size) =>
            AddFile(TransferPaths.Join(directory, name), size);

        public int Enumerations(string path) => _nodes.TryGetValue(Key(path), out var node) ? node.Enumerations : 0;

        public long Length(string path) => _nodes[Key(path)].Data.Length;

        public bool FileExists(string path) => _nodes.TryGetValue(Key(path), out var node) && !node.IsDirectory;

        public bool DirectoryExists(string path) => _nodes.TryGetValue(Key(path), out var node) && node.IsDirectory;

        public bool TryDescribe(string path, out TransferEntry entry, out string? linkTarget)
        {
            entry = default;
            linkTarget = null;
            if (!_nodes.TryGetValue(Key(path), out var node))
                return false;

            linkTarget = node.LinkTarget;
            entry = new TransferEntry(Name(path), node.IsDirectory, node.Data.Length, node.IsReparse, node.LinkTarget, node.ReadOnly);
            return true;
        }

        public IEnumerable<TransferEntry> Enumerate(string directory)
        {
            var node = _nodes[Key(directory)];
            node.Enumerations++;
            foreach (var pair in _nodes.ToList())
            {
                if (!IsChild(Key(directory), pair.Key))
                    continue;
                var child = pair.Value;
                yield return new TransferEntry(Name(pair.Key), child.IsDirectory, child.Data.Length, child.IsReparse, child.LinkTarget, child.ReadOnly);
            }
        }

        public TransferTimestamps GetTimestamps(string path) => default;

        public void CopyFile(string source, string destination, Action<long> onBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(ThrowOn, source, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The file is in use.") { HResult = unchecked((int)0x80070020) };

            var data = _nodes[Key(source)].Data.ToArray();
            _nodes[Key(destination)] = new Node { Data = data };
            onBytes(data.Length);
            Copies++;
            AfterCopy?.Invoke();
        }

        public void MovePath(string source, string destination, bool isDirectory)
        {
            Moves++;
            var from = Key(source);
            var node = _nodes[from];
            _nodes.Remove(from);
            _nodes[Key(destination)] = node;
            if (!isDirectory)
                return;

            foreach (var pair in _nodes.Keys.ToList())
            {
                if (!IsChild(from, pair))
                    continue;
                var child = _nodes[pair];
                _nodes.Remove(pair);
                _nodes[Key(destination) + pair[from.Length..]] = child;
            }
        }

        public void DeleteFile(string path)
        {
            Deletes++;
            _nodes.Remove(Key(path));
            AfterDelete?.Invoke();
        }

        public void DeleteDirectoryEntry(string path)
        {
            Deletes++;
            _nodes.Remove(Key(path));
        }

        public void CreateDirectory(string path) => AddDirectory(path);

        public void CopyLink(string linkTarget, string destination, bool isDirectory)
        {
            LinksCopied++;
            AddLink(destination, linkTarget, isDirectory);
        }

        public void CopyMetadata(string source, string destination, bool isDirectory)
        {
        }

        public void ClearReadOnly(string path)
        {
            ReadOnlyClears++;
            if (_nodes.TryGetValue(Key(path), out var node))
                node.ReadOnly = false;
        }

        private static string Key(string path) => TransferPaths.Normalize(path);

        private static string Name(string path)
        {
            var key = Key(path);
            var index = key.LastIndexOf('\\');
            return index < 0 ? key : key[(index + 1)..];
        }

        private static bool IsChild(string parent, string child)
        {
            var prefix = parent.TrimEnd('\\') + "\\";
            if (!child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            return child[prefix.Length..].IndexOf('\\') < 0;
        }

        private sealed class Node
        {
            public bool IsDirectory { get; set; }

            public bool IsReparse { get; set; }

            public string? LinkTarget { get; set; }

            public byte[] Data { get; set; } = [];

            public bool ReadOnly { get; set; }

            public int Enumerations { get; set; }
        }
    }
}
