namespace FolderScope;

public sealed class FolderNode
{
    private readonly object _gate = new();
    private readonly List<FolderNode> _subfolders = new();
    private readonly List<FileEntry> _files = new();
    private long _totalSize;
    private long _totalFiles;
    private long _totalFolders;
    private int _denied;
    private int _childrenVersion;
    private volatile bool _accessDenied;
    private volatile bool _incomplete;
    private volatile bool _enumerationComplete;
    private string? _error;

    public FolderNode(string name, string fullPath, FolderNode? parent)
    {
        Name = name;
        FullPath = fullPath;
        Parent = parent;
    }

    public string Name { get; internal set; }

    public string FullPath { get; internal set; }

    public FolderNode? Parent { get; }

    public bool IsReparsePoint { get; internal set; }

    public string? LinkTarget { get; internal set; }

    public DateTime LastWriteTimeUtc { get; private set; }

    public DateTime CreationTimeUtc { get; private set; }

    public DateTime LastAccessTimeUtc { get; private set; }

    public bool AccessDenied => _accessDenied;

    public bool Incomplete => _incomplete;

    public bool EnumerationComplete => _enumerationComplete;

    public string? Error => _error;

    public long ReadSize() => Interlocked.Read(ref _totalSize);

    public long ReadFiles() => Interlocked.Read(ref _totalFiles);

    public long ReadFolders() => Interlocked.Read(ref _totalFolders);

    public int ReadDenied() => Volatile.Read(ref _denied);

    public int ReadChildrenVersion()
    {
        lock (_gate)
            return _childrenVersion;
    }

    public bool HasChildren
    {
        get
        {
            lock (_gate)
                return _subfolders.Count > 0 || _files.Count > 0;
        }
    }

    internal void SetTimestamps(DateTime lastWriteTimeUtc, DateTime creationTimeUtc, DateTime lastAccessTimeUtc)
    {
        LastWriteTimeUtc = lastWriteTimeUtc;
        CreationTimeUtc = creationTimeUtc;
        LastAccessTimeUtc = lastAccessTimeUtc;
    }

    internal void MarkEnumerated() => _enumerationComplete = true;

    internal void MarkIncomplete() => _incomplete = true;

    internal void Fail(string message)
    {
        _error = string.IsNullOrWhiteSpace(message) ? "Could not read this folder." : message.Trim();
        _enumerationComplete = true;
    }

    internal void MarkAccessDenied()
    {
        if (_accessDenied)
            return;

        _accessDenied = true;
        _error = "Access denied.";
        for (var node = this; node != null; node = node.Parent)
            Interlocked.Increment(ref node._denied);
    }

    internal void Publish(List<FolderNode> folders, List<FileEntry> files)
    {
        if (folders.Count == 0 && files.Count == 0)
            return;

        lock (_gate)
        {
            if (folders.Count > 0)
                _subfolders.AddRange(folders);
            if (files.Count > 0)
                _files.AddRange(files);
            _childrenVersion++;
        }
    }

    internal void AddTotals(long bytes, long files, long folders)
    {
        for (var node = this; node != null; node = node.Parent)
        {
            if (bytes != 0)
                Interlocked.Add(ref node._totalSize, bytes);
            if (files != 0)
                Interlocked.Add(ref node._totalFiles, files);
            if (folders != 0)
                Interlocked.Add(ref node._totalFolders, folders);
        }
    }

    internal void CopyChildren(List<FolderNode> folders, List<FileEntry> files)
    {
        lock (_gate)
        {
            folders.AddRange(_subfolders);
            files.AddRange(_files);
        }
    }

    internal void Touch()
    {
        lock (_gate)
            _childrenVersion++;
    }

    internal void SetOwnTotals(long bytes, long files, long folders)
    {
        Interlocked.Exchange(ref _totalSize, bytes);
        Interlocked.Exchange(ref _totalFiles, files);
        Interlocked.Exchange(ref _totalFolders, folders);
    }

    internal void Retarget(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
        List<FolderNode> folders;
        lock (_gate)
        {
            folders = _subfolders.ToList();
            _childrenVersion++;
        }

        foreach (var child in folders)
            child.Retarget(child.Name, TransferPaths.Join(fullPath, child.Name));
    }

    internal bool RemoveFile(string name, out FileEntry removed)
    {
        lock (_gate)
        {
            var index = _files.FindIndex(file => string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                removed = default;
                return false;
            }

            removed = _files[index];
            _files.RemoveAt(index);
            _childrenVersion++;
        }

        AddTotals(-removed.Size, -1, 0);
        return true;
    }

    internal bool RemoveFolder(string name, out FolderNode? removed)
    {
        lock (_gate)
        {
            var index = _subfolders.FindIndex(folder => string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                removed = null;
                return false;
            }

            removed = _subfolders[index];
            _subfolders.RemoveAt(index);
            _childrenVersion++;
        }

        AddTotals(-removed.ReadSize(), -removed.ReadFiles(), -(removed.ReadFolders() + 1));
        return true;
    }

    internal bool ReplaceFile(FileEntry file)
    {
        long delta;
        long fileDelta;
        lock (_gate)
        {
            var index = _files.FindIndex(entry => string.Equals(entry.Name, file.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                _files.Add(file);
                delta = file.Size;
                fileDelta = 1;
            }
            else
            {
                delta = file.Size - _files[index].Size;
                fileDelta = 0;
                _files[index] = file;
            }

            _childrenVersion++;
        }

        if (delta != 0 || fileDelta != 0)
            AddTotals(delta, fileDelta, 0);
        return true;
    }

    internal void AddSubfolder(FolderNode child)
    {
        lock (_gate)
        {
            _subfolders.Add(child);
            _childrenVersion++;
        }

        AddTotals(child.ReadSize(), child.ReadFiles(), child.ReadFolders() + 1);
    }
}
