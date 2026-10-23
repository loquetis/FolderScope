namespace FolderScope;

public readonly record struct FsEntry(
    string Name,
    bool IsDirectory,
    long Length,
    FileAttributes Attributes,
    string? LinkTarget,
    DateTime LastWriteTimeUtc = default,
    DateTime CreationTimeUtc = default,
    DateTime LastAccessTimeUtc = default);
