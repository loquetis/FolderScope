namespace FolderScope;

public readonly record struct TransferEntry(
    string Name,
    bool IsDirectory,
    long Length,
    bool IsReparse,
    string? LinkTarget,
    bool ReadOnly);

public readonly record struct TransferTimestamps(DateTime LastWriteTimeUtc, DateTime CreationTimeUtc, DateTime LastAccessTimeUtc);

public interface ITransferIo
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    bool TryDescribe(string path, out TransferEntry entry, out string? linkTarget);

    IEnumerable<TransferEntry> Enumerate(string directory);

    TransferTimestamps GetTimestamps(string path);

    void CopyFile(string source, string destination, Action<long> onBytes, CancellationToken cancellationToken);

    void MovePath(string source, string destination, bool isDirectory);

    void DeleteFile(string path);

    void DeleteDirectoryEntry(string path);

    void CreateDirectory(string path);

    void CopyLink(string linkTarget, string destination, bool isDirectory);

    void CopyMetadata(string source, string destination, bool isDirectory);

    void ClearReadOnly(string path);
}
