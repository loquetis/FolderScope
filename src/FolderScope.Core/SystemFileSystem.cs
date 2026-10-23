namespace FolderScope;

public sealed class SystemFileSystem : IFileSystem
{
    public static EnumerationOptions EnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        // The default skips Hidden and System, which would under-count a drive.
        AttributesToSkip = FileAttributes.None,
        ReturnSpecialDirectories = false,
        BufferSize = 64 * 1024
    };

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(PathHelper.ForIo(path));
        }
        catch (Exception ex) when (IsProbeFailure(ex))
        {
            return false;
        }
    }

    public IEnumerable<FsEntry> Enumerate(string path)
    {
        var directory = new DirectoryInfo(PathHelper.ForIo(path));
        foreach (var entry in directory.EnumerateFileSystemInfos("*", EnumerationOptions))
        {
            if (TryMap(entry, out var mapped))
                yield return mapped;
        }
    }

    private static bool TryMap(FileSystemInfo entry, out FsEntry mapped)
    {
        mapped = default;
        try
        {
            var isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
            long length = 0;
            if (!isDirectory && entry is FileInfo file)
            {
                try
                {
                    length = file.Length;
                }
                catch (Exception ex) when (IsProbeFailure(ex))
                {
                    length = 0;
                }
            }

            string? link = null;
            try
            {
                if (!string.IsNullOrEmpty(entry.LinkTarget))
                    link = entry.LinkTarget;
            }
            catch (Exception ex) when (IsProbeFailure(ex))
            {
                link = null;
            }

            mapped = new FsEntry(
                entry.Name,
                isDirectory,
                length,
                entry.Attributes,
                link,
                ReadStamp(entry, static item => item.LastWriteTimeUtc),
                ReadStamp(entry, static item => item.CreationTimeUtc),
                ReadStamp(entry, static item => item.LastAccessTimeUtc));
            return true;
        }
        catch (Exception ex) when (IsProbeFailure(ex))
        {
            return false;
        }
    }

    private static DateTime ReadStamp(FileSystemInfo entry, Func<FileSystemInfo, DateTime> read)
    {
        try
        {
            return read(entry);
        }
        catch (Exception ex) when (IsProbeFailure(ex))
        {
            return default;
        }
    }

    private static bool IsProbeFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or ArgumentException
            or NotSupportedException
            or InvalidOperationException;
}
