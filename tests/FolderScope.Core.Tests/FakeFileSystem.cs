namespace FolderScope.Core.Tests;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, FsEntry[]> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _denied = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _ioErrors = new(StringComparer.OrdinalIgnoreCase);

    public ManualResetEventSlim? Pause { get; set; }

    public int PauseAfterYields { get; set; } = int.MaxValue;

    public string? PausePath { get; set; }

    public void AddDirectory(string path, params FsEntry[] entries) => _directories[path] = entries;

    public void AddFile(string path) => _files.Add(path);

    public void Deny(string path) => _denied.Add(path);

    public void Fail(string path, string message) => _ioErrors[path] = message;

    public bool FileExists(string path) => _files.Contains(path);

    public IEnumerable<FsEntry> Enumerate(string path)
    {
        if (_denied.Contains(path))
            throw new UnauthorizedAccessException("Access denied.");

        if (_ioErrors.TryGetValue(path, out var message))
            throw new IOException(message);

        if (!_directories.TryGetValue(path, out var entries))
            throw new DirectoryNotFoundException(path);

        var yielded = 0;
        foreach (var entry in entries)
        {
            yield return entry;
            yielded++;
            if (Pause != null
                && yielded == PauseAfterYields
                && (PausePath == null || string.Equals(PausePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                Pause.Wait();
            }
        }
    }
}
