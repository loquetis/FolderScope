namespace FolderScope;

public sealed class ScanProgress
{
    private long _files;
    private long _directories;
    private long _bytes;
    private string? _current = "";

    public long FilesScanned => Interlocked.Read(ref _files);

    public long DirectoriesScanned => Interlocked.Read(ref _directories);

    public long BytesScanned => Interlocked.Read(ref _bytes);

    public string CurrentPath => Volatile.Read(ref _current) ?? "";

    public void AddFiles(long count, long bytes)
    {
        if (count != 0)
            Interlocked.Add(ref _files, count);
        if (bytes != 0)
            Interlocked.Add(ref _bytes, bytes);
    }

    public void AddDirectory() => Interlocked.Increment(ref _directories);

    public void SetCurrent(string path) => Volatile.Write(ref _current, path);
}
