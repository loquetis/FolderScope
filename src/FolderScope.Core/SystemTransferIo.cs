namespace FolderScope;

public sealed class SystemTransferIo : ITransferIo
{
    private static EnumerationOptions Options { get; } = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.None,
        ReturnSpecialDirectories = false
    };

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(Io(path));
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return false;
        }
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(Io(path));
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return false;
        }
    }

    public bool TryDescribe(string path, out TransferEntry entry, out string? linkTarget)
    {
        entry = default;
        linkTarget = null;
        try
        {
            var io = Io(path);
            if (!File.Exists(io) && !Directory.Exists(io))
                return false;

            var attributes = File.GetAttributes(io);
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var reparse = (attributes & FileAttributes.ReparsePoint) != 0;
            if (reparse)
                linkTarget = ReadLink(io, isDirectory);

            long length = 0;
            if (!isDirectory && !reparse)
            {
                try
                {
                    length = new FileInfo(io).Length;
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                    length = 0;
                }
            }

            entry = new TransferEntry(NameOf(path), isDirectory, length, reparse, linkTarget, (attributes & FileAttributes.ReadOnly) != 0);
            return true;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return false;
        }
    }

    public IEnumerable<TransferEntry> Enumerate(string directory)
    {
        foreach (var info in new DirectoryInfo(Io(directory)).EnumerateFileSystemInfos("*", Options))
        {
            var isDirectory = (info.Attributes & FileAttributes.Directory) != 0;
            var reparse = (info.Attributes & FileAttributes.ReparsePoint) != 0;
            string? link = null;
            if (reparse)
            {
                try
                {
                    link = info.LinkTarget;
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                    link = null;
                }
            }

            long length = 0;
            if (!isDirectory && !reparse && info is FileInfo file)
            {
                try
                {
                    length = file.Length;
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                    length = 0;
                }
            }

            yield return new TransferEntry(
                info.Name,
                isDirectory,
                length,
                reparse,
                link,
                (info.Attributes & FileAttributes.ReadOnly) != 0);
        }
    }

    public TransferTimestamps GetTimestamps(string path)
    {
        var io = Io(path);
        var info = Directory.Exists(io) ? (FileSystemInfo)new DirectoryInfo(io) : new FileInfo(io);
        return new TransferTimestamps(info.LastWriteTimeUtc, info.CreationTimeUtc, info.LastAccessTimeUtc);
    }

    public void CopyFile(string source, string destination, Action<long> onBytes, CancellationToken cancellationToken)
    {
        var from = Io(source);
        var to = Io(destination);
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(Io(directory));

        var completed = false;
        try
        {
            const int bufferSize = 1024 * 1024;
            using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
            using var output = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
            var buffer = new byte[bufferSize];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
                onBytes(read);
            }

            completed = true;
        }
        finally
        {
            if (!completed)
            {
                try
                {
                    if (File.Exists(to))
                        File.Delete(to);
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                }
            }
        }
    }

    public void MovePath(string source, string destination, bool isDirectory)
    {
        if (isDirectory)
            Directory.Move(Io(source), Io(destination));
        else
            File.Move(Io(source), Io(destination));
    }

    public void DeleteFile(string path)
    {
        ClearReadOnly(path);
        File.Delete(Io(path));
    }

    public void DeleteDirectoryEntry(string path)
    {
        var io = Io(path);
        if (Directory.Exists(io))
        {
            try
            {
                var attributes = File.GetAttributes(io);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(io, attributes & ~FileAttributes.ReadOnly);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
            }

            Directory.Delete(io, recursive: false);
            return;
        }

        if (File.Exists(io))
            DeleteFile(path);
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(Io(path));

    public void CopyLink(string linkTarget, string destination, bool isDirectory)
    {
        if (isDirectory)
            Directory.CreateSymbolicLink(Io(destination), linkTarget);
        else
            File.CreateSymbolicLink(Io(destination), linkTarget);
    }

    public void CopyMetadata(string source, string destination, bool isDirectory)
    {
        var from = Io(source);
        var to = Io(destination);
        var sourceInfo = isDirectory ? (FileSystemInfo)new DirectoryInfo(from) : new FileInfo(from);
        var destInfo = isDirectory ? (FileSystemInfo)new DirectoryInfo(to) : new FileInfo(to);
        try
        {
            destInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
            destInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
            destInfo.LastAccessTimeUtc = sourceInfo.LastAccessTimeUtc;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
        }

        try
        {
            var attributes = sourceInfo.Attributes & ~FileAttributes.Directory;
            if (attributes != 0)
                File.SetAttributes(to, attributes);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
        }
    }

    public void ClearReadOnly(string path)
    {
        try
        {
            var io = Io(path);
            var attributes = File.GetAttributes(io);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(io, attributes & ~FileAttributes.ReadOnly);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
        }
    }

    private static string Io(string path) => PathHelper.ForIo(path);

    private static string? ReadLink(string ioPath, bool isDirectory)
    {
        try
        {
            FileSystemInfo info = isDirectory ? new DirectoryInfo(ioPath) : new FileInfo(ioPath);
            return string.IsNullOrEmpty(info.LinkTarget) ? null : info.LinkTarget;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return null;
        }
    }

    private static string NameOf(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var sep = TransferPaths.Separator(trimmed);
        var index = trimmed.LastIndexOf(sep);
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    private static bool IsExpected(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
