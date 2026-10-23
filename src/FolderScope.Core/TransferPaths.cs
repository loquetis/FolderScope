namespace FolderScope;

public static class TransferPaths
{
    public static string Join(string directory, string name)
    {
        if (directory.Length == 0)
            return name;

        var sep = Separator(directory);
        if (directory[^1] == sep)
            return directory + name;

        return directory + sep + name;
    }

    public static char Separator(string path)
    {
        if (path.Contains('\\') && !path.Contains('/'))
            return '\\';

        if (path.Length >= 2 && path[1] == ':')
            return '\\';

        return Path.DirectorySeparatorChar;
    }

    public static string Normalize(string path)
    {
        var trimmed = path.Trim().Trim('"');
        if (trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            trimmed = @"\\" + trimmed[8..];
        else if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal))
            trimmed = trimmed[4..];

        var sep = Separator(trimmed);
        var root = VolumeKey(trimmed);
        if (root != null && IsRoot(trimmed, root))
            return root;

        return trimmed.TrimEnd(sep);
    }

    public static bool SamePath(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    public static bool IsSameOrInside(string parent, string child)
    {
        var parentPath = Normalize(parent);
        var childPath = Normalize(child);
        if (string.Equals(parentPath, childPath, StringComparison.OrdinalIgnoreCase))
            return true;

        var sep = Separator(parentPath);
        var prefix = parentPath.TrimEnd(sep) + sep;
        return childPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string? VolumeKey(string path)
    {
        var trimmed = path.Trim().Trim('"');
        if (trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            trimmed = @"\\" + trimmed[8..];
        else if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal))
            trimmed = trimmed[4..];

        trimmed = trimmed.Replace('/', '\\');
        if (trimmed.Length >= 2 && trimmed[1] == ':')
            return char.ToUpperInvariant(trimmed[0]) + @":\";

        if (!trimmed.StartsWith(@"\\", StringComparison.Ordinal))
            return null;

        var rest = trimmed.TrimStart('\\');
        var slash = rest.IndexOf('\\');
        if (slash <= 0)
            return null;

        var server = rest[..slash];
        var after = rest[(slash + 1)..];
        var slash2 = after.IndexOf('\\');
        var share = slash2 < 0 ? after.TrimEnd('\\') : after[..slash2];
        if (server.Length == 0 || share.Length == 0)
            return null;

        return @"\\" + server + @"\" + share;
    }

    public static bool SameVolume(string left, string right)
    {
        var a = VolumeKey(left);
        var b = VolumeKey(right);
        return a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoot(string path, string root)
    {
        var sep = Separator(path.Contains('\\') ? path : root);
        return string.Equals(
            path.TrimEnd(sep),
            root.TrimEnd(sep),
            StringComparison.OrdinalIgnoreCase);
    }
}
