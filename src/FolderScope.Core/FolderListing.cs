namespace FolderScope;

public enum FolderColumn
{
    Name,
    Size,
    Percent,
    Files,
    Modified,
    MatchSize,
    MatchFiles
}

public enum FolderSortDirection
{
    Ascending,
    Descending
}

public readonly struct ListingRow
{
    private readonly FolderNode? _folder;
    private readonly string _parentPath;
    private readonly FileEntry _file;
    private readonly bool _isFile;

    private ListingRow(FolderNode folder, long sortSize, long sortFiles, FolderMatch match)
    {
        _folder = folder;
        _parentPath = folder.Parent?.FullPath ?? "";
        _file = default;
        _isFile = false;
        SortSize = sortSize;
        SortFiles = sortFiles;
        MatchBytes = match.Bytes;
        MatchFiles = match.Files;
        ModifiedUtc = folder.LastWriteTimeUtc;
        SortModified = ModifiedUtc == default ? 0 : ModifiedUtc.Ticks;
    }

    private ListingRow(string parentPath, FileEntry file, bool matched)
    {
        _folder = null;
        _parentPath = parentPath;
        _file = file;
        _isFile = true;
        SortSize = file.Size;
        SortFiles = 1;
        MatchBytes = matched ? file.Size : 0;
        MatchFiles = matched ? 1 : 0;
        ModifiedUtc = file.LastWriteTimeUtc;
        SortModified = ModifiedUtc == default ? 0 : ModifiedUtc.Ticks;
    }

    public long SortSize { get; }

    public long SortFiles { get; }

    public long SortModified { get; }

    public long MatchBytes { get; }

    public long MatchFiles { get; }

    public DateTime ModifiedUtc { get; }

    public bool IsDirectory => !_isFile;

    public string Name => _isFile ? _file.Name : _folder!.Name;

    public string FullPath => _isFile ? PathHelper.Combine(_parentPath, _file.Name) : _folder!.FullPath;

    public long LiveSize => _isFile ? _file.Size : _folder!.ReadSize();

    public long LiveFiles => _isFile ? 1 : _folder!.ReadFiles();

    public bool IsReparsePoint => _isFile ? _file.IsReparsePoint : _folder!.IsReparsePoint;

    public bool AccessDenied => !_isFile && _folder!.AccessDenied;

    public string? LinkTarget => _isFile ? _file.LinkTarget : _folder!.LinkTarget;

    public string? Error => _isFile ? null : _folder!.Error;

    public int DeniedCount => _isFile ? 0 : _folder!.ReadDenied();

    public FolderNode? Folder => _folder;

    public string DisplayName
    {
        get
        {
            if (IsReparsePoint && !string.IsNullOrEmpty(LinkTarget))
                return Name + "  →  " + LinkTarget;
            if (AccessDenied)
                return Name + "  (access denied)";
            return Name;
        }
    }

    public static ListingRow ForFolder(FolderNode folder, FolderMatch match = default) =>
        new(folder, folder.ReadSize(), folder.ReadFiles(), match);

    public static ListingRow ForFile(FolderNode parent, FileEntry file, DateFilter filter = default)
    {
        var matched = filter.IsActive
            && filter.Matches(file.LastWriteTimeUtc, file.CreationTimeUtc, file.LastAccessTimeUtc);
        return new ListingRow(parent.FullPath, file, matched);
    }
}

public static class FolderListing
{
    public static ListingRow[] Build(
        FolderNode folder,
        string? filter,
        FolderColumn column,
        FolderSortDirection direction,
        DateFilter dateFilter = default) =>
        Build(folder, filter, column, direction, dateFilter, out _);

    public static ListingRow[] Build(
        FolderNode folder,
        string? filter,
        FolderColumn column,
        FolderSortDirection direction,
        DateFilter dateFilter,
        out FolderMatch match)
    {
        var folders = new List<FolderNode>();
        var files = new List<FileEntry>();
        folder.CopyChildren(folders, files);

        Dictionary<FolderNode, FolderMatch>? matches = null;
        match = default;
        if (dateFilter.IsActive)
        {
            matches = DateRollup.Compute(folder, dateFilter);
            if (matches.TryGetValue(folder, out var current))
                match = current;
        }

        var rows = new List<ListingRow>(folders.Count + files.Count);
        foreach (var sub in folders)
        {
            if (!Matches(sub.Name, filter))
                continue;

            var subMatch = default(FolderMatch);
            if (matches != null)
                matches.TryGetValue(sub, out subMatch);
            rows.Add(ListingRow.ForFolder(sub, subMatch));
        }

        foreach (var file in files)
        {
            if (Matches(file.Name, filter))
                rows.Add(ListingRow.ForFile(folder, file, dateFilter));
        }

        rows.Sort((a, b) => Compare(a, b, column, direction));
        return rows.ToArray();
    }

    public static int Compare(ListingRow a, ListingRow b, FolderColumn column, FolderSortDirection direction)
    {
        var sign = direction == FolderSortDirection.Ascending ? 1 : -1;
        var primary = column switch
        {
            FolderColumn.Name => NaturalSort.Compare(a.Name, b.Name),
            FolderColumn.Size => a.SortSize.CompareTo(b.SortSize),
            FolderColumn.Percent => a.SortSize.CompareTo(b.SortSize),
            FolderColumn.Files => a.SortFiles.CompareTo(b.SortFiles),
            FolderColumn.Modified => a.SortModified.CompareTo(b.SortModified),
            FolderColumn.MatchSize => a.MatchBytes.CompareTo(b.MatchBytes),
            FolderColumn.MatchFiles => a.MatchFiles.CompareTo(b.MatchFiles),
            _ => 0
        };

        if (primary != 0)
            return primary * sign;

        var name = NaturalSort.Compare(a.Name, b.Name);
        if (name != 0)
            return name;

        return string.CompareOrdinal(a.FullPath, b.FullPath);
    }

    private static bool Matches(string name, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
