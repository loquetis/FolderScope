namespace FolderScope;

public readonly record struct FileEntry(
    string Name,
    long Size,
    bool IsReparsePoint,
    string? LinkTarget,
    DateTime LastWriteTimeUtc = default,
    DateTime CreationTimeUtc = default,
    DateTime LastAccessTimeUtc = default);
