namespace FolderScope;

public enum FileColumn
{
    Name,
    Path,
    Size,
    Date
}

public readonly record struct FileHit(
    string Name,
    string DirectoryPath,
    string FullPath,
    long Size,
    DateTime ModifiedUtc,
    DateTime CreatedUtc,
    DateTime AccessedUtc,
    DateField Field,
    bool IsReparsePoint,
    string? LinkTarget)
{
    public DateTime SelectedUtc => Field switch
    {
        DateField.Created => CreatedUtc,
        DateField.Accessed => AccessedUtc,
        _ => ModifiedUtc
    };

    public string DisplayName =>
        IsReparsePoint && !string.IsNullOrEmpty(LinkTarget)
            ? Name + "  →  " + LinkTarget
            : Name;
}

public readonly struct FileListResult
{
    public FileHit[] Files { get; init; }

    public long MatchBytes { get; init; }

    public long MatchFiles { get; init; }
}

public static class FileListing
{
    public static FileListResult Collect(
        FolderNode folder,
        DateFilter dateFilter,
        string? nameFilter,
        FileColumn column,
        FolderSortDirection direction)
    {
        var hits = new List<FileHit>();
        long bytes = 0;
        long count = 0;
        Walk(folder, dateFilter.IsActive, dateFilter, nameFilter, hits, ref bytes, ref count);
        hits.Sort((a, b) => Compare(a, b, column, direction));
        return new FileListResult
        {
            Files = hits.ToArray(),
            MatchBytes = bytes,
            MatchFiles = count
        };
    }

    public static int Compare(FileHit a, FileHit b, FileColumn column, FolderSortDirection direction)
    {
        var sign = direction == FolderSortDirection.Ascending ? 1 : -1;
        var primary = column switch
        {
            FileColumn.Name => NaturalSort.Compare(a.Name, b.Name),
            FileColumn.Path => NaturalSort.Compare(a.FullPath, b.FullPath),
            FileColumn.Size => a.Size.CompareTo(b.Size),
            FileColumn.Date => SelectedTicks(a).CompareTo(SelectedTicks(b)),
            _ => 0
        };

        if (primary != 0)
            return primary * sign;

        var name = NaturalSort.Compare(a.Name, b.Name);
        if (name != 0)
            return name;

        return string.CompareOrdinal(a.FullPath, b.FullPath);
    }

    private static long SelectedTicks(FileHit hit) =>
        hit.SelectedUtc == default ? 0 : hit.SelectedUtc.Ticks;

    private static void Walk(
        FolderNode node,
        bool recursive,
        DateFilter filter,
        string? nameFilter,
        List<FileHit> hits,
        ref long bytes,
        ref long count)
    {
        if (node.IsReparsePoint)
            return;

        var folders = new List<FolderNode>();
        var files = new List<FileEntry>();
        node.CopyChildren(folders, files);

        foreach (var file in files)
        {
            var dateOk = !filter.IsActive || filter.Matches(file.LastWriteTimeUtc, file.CreationTimeUtc, file.LastAccessTimeUtc);
            if (!dateOk)
                continue;

            if (filter.IsActive)
            {
                bytes += file.Size;
                count++;
            }

            if (!NameMatches(file.Name, node.FullPath, nameFilter))
                continue;

            hits.Add(new FileHit(
                file.Name,
                node.FullPath,
                PathHelper.Combine(node.FullPath, file.Name),
                file.Size,
                file.LastWriteTimeUtc,
                file.CreationTimeUtc,
                file.LastAccessTimeUtc,
                filter.Field,
                file.IsReparsePoint,
                file.LinkTarget));
        }

        if (!recursive)
            return;

        foreach (var child in folders)
            Walk(child, true, filter, nameFilter, hits, ref bytes, ref count);
    }

    private static bool NameMatches(string name, string directory, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var needle = filter.Trim();
        return name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || directory.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
