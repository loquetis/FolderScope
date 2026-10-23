namespace FolderScope;

public readonly record struct FolderMatch(long Bytes, long Files);

public static class DateRollup
{
    public static Dictionary<FolderNode, FolderMatch> Compute(FolderNode root, DateFilter filter)
    {
        var map = new Dictionary<FolderNode, FolderMatch>();
        Walk(root, filter, map);
        return map;
    }

    private static FolderMatch Walk(FolderNode node, DateFilter filter, Dictionary<FolderNode, FolderMatch> map)
    {
        if (node.IsReparsePoint)
        {
            var empty = default(FolderMatch);
            map[node] = empty;
            return empty;
        }

        var folders = new List<FolderNode>();
        var files = new List<FileEntry>();
        node.CopyChildren(folders, files);

        long bytes = 0;
        long count = 0;
        if (filter.IsActive)
        {
            foreach (var file in files)
            {
                if (!filter.Matches(file.LastWriteTimeUtc, file.CreationTimeUtc, file.LastAccessTimeUtc))
                    continue;

                bytes += file.Size;
                count++;
            }
        }

        foreach (var child in folders)
        {
            var childMatch = Walk(child, filter, map);
            bytes += childMatch.Bytes;
            count += childMatch.Files;
        }

        var match = new FolderMatch(bytes, count);
        map[node] = match;
        return match;
    }
}
