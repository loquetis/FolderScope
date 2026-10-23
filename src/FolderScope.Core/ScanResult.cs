namespace FolderScope;

public sealed class ScanResult
{
    public ScanResult(FolderNode root, bool cancelled, TimeSpan elapsed, ScanProgress progress)
    {
        Root = root;
        Cancelled = cancelled;
        Elapsed = elapsed;
        Progress = progress;
    }

    public FolderNode Root { get; }

    public bool Cancelled { get; }

    public TimeSpan Elapsed { get; }

    public ScanProgress Progress { get; }
}
