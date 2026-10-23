namespace FolderScope;

public static class TreeLookup
{
    public static FolderNode? Find(FolderNode? root, string path)
    {
        if (root == null)
            return null;

        if (TransferPaths.SamePath(root.FullPath, path))
            return root;

        if (!TransferPaths.IsSameOrInside(root.FullPath, path))
            return null;

        var folders = new List<FolderNode>();
        var files = new List<FileEntry>();
        root.CopyChildren(folders, files);
        foreach (var folder in folders)
        {
            var found = Find(folder, path);
            if (found != null)
                return found;
        }

        return null;
    }
}

public static class TreePatch
{
    public static void Apply(FolderNode root, IReadOnlyList<TransferChange> changes, Func<string, FolderNode?>? lookup)
    {
        foreach (var change in changes)
        {
            if (change.Created && !string.IsNullOrEmpty(change.Destination))
                Add(root, change, lookup);
            if (change.SourceRemoved)
                Remove(root, change.Source);
        }
    }

    public static bool Remove(FolderNode root, string path)
    {
        var parentPath = Parent(path);
        if (parentPath == null)
            return false;

        var parent = TreeLookup.Find(root, parentPath);
        if (parent == null)
            return false;

        var name = Name(path);
        if (parent.RemoveFolder(name, out _))
            return true;

        return parent.RemoveFile(name, out _);
    }

    private static void Add(FolderNode root, TransferChange change, Func<string, FolderNode?>? lookup)
    {
        var destination = change.Destination!;
        var parent = TreeLookup.Find(root, Parent(destination) ?? "");
        if (parent == null)
            return;

        var name = Name(destination);
        if (!change.IsDirectory)
        {
            parent.ReplaceFile(new FileEntry(
                name,
                change.Bytes,
                false,
                null,
                change.Timestamps.LastWriteTimeUtc,
                change.Timestamps.CreationTimeUtc,
                change.Timestamps.LastAccessTimeUtc));
            return;
        }

        if (TreeLookup.Find(parent, destination) != null)
            return;

        var source = lookup?.Invoke(change.Source);
        var clone = source == null ? EmptyFolder(name, destination, parent) : Clone(source, parent, name, destination);
        parent.AddSubfolder(clone);
    }

    private static FolderNode EmptyFolder(string name, string fullPath, FolderNode parent)
    {
        var folder = new FolderNode(name, fullPath, parent);
        folder.MarkEnumerated();
        folder.MarkIncomplete();
        return folder;
    }

    private static FolderNode Clone(FolderNode source, FolderNode parent, string name, string fullPath)
    {
        var folder = new FolderNode(name, fullPath, parent);
        folder.IsReparsePoint = source.IsReparsePoint;
        folder.LinkTarget = source.LinkTarget;
        folder.SetTimestamps(source.LastWriteTimeUtc, source.CreationTimeUtc, source.LastAccessTimeUtc);
        if (source.AccessDenied)
            folder.MarkAccessDenied();
        if (source.Incomplete)
            folder.MarkIncomplete();

        var subfolders = new List<FolderNode>();
        var files = new List<FileEntry>();
        source.CopyChildren(subfolders, files);
        var cloned = new List<FolderNode>(subfolders.Count);
        foreach (var child in subfolders)
            cloned.Add(Clone(child, folder, child.Name, TransferPaths.Join(fullPath, child.Name)));

        folder.Publish(cloned, files.ToList());
        folder.SetOwnTotals(source.ReadSize(), source.ReadFiles(), source.ReadFolders());
        if (source.EnumerationComplete)
            folder.MarkEnumerated();
        return folder;
    }

    private static string Name(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var sep = TransferPaths.Separator(trimmed);
        var index = trimmed.LastIndexOf(sep);
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    private static string? Parent(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var sep = TransferPaths.Separator(trimmed);
        var index = trimmed.LastIndexOf(sep);
        if (index <= 0)
            return null;

        var parent = trimmed[..index];
        return parent.Length == 2 && parent[1] == ':' ? parent + sep : parent;
    }
}
