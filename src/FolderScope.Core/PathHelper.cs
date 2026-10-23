namespace FolderScope;

public static class PathHelper
{
    public static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is empty.", nameof(path));

        var trimmed = path.Trim().Trim('"');
        if (OperatingSystem.IsWindows())
            trimmed = StripLongPathPrefix(trimmed);

        var full = Path.GetFullPath(trimmed);
        if (OperatingSystem.IsWindows())
            full = StripLongPathPrefix(full);

        var root = Path.GetPathRoot(full);
        if (IsPathRoot(full, root))
            return FormatRoot(root ?? full);

        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string Combine(string directory, string name)
    {
        if (directory == "/")
            return "/" + name;

        // Path.Combine("C:", "Windows") yields "C:Windows", not "C:\Windows".
        if (directory.Length == 2 && directory[1] == ':')
            return directory + "\\" + name;

        return Path.Combine(directory, name);
    }

    public static string DisplayName(string path)
    {
        var root = Path.GetPathRoot(path);
        if (IsPathRoot(path, root))
            return FormatRoot(root ?? path);

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    public static string ForIo(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;

        return ToLongPathSyntax(Path.GetFullPath(path));
    }

    /// <summary>
    /// Prefixes a full Windows path so Win32 APIs accept paths beyond MAX_PATH.
    /// </summary>
    public static string ToLongPathSyntax(string fullPath)
    {
        if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            return fullPath;

        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + fullPath[2..];

        return @"\\?\" + fullPath;
    }

    public static string StripLongPathPrefix(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return @"\\" + path[8..];

        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path[4..];

        return path;
    }

    internal static bool IsPathRoot(string path, string? root)
    {
        if (string.IsNullOrEmpty(root))
            return false;

        var pathTrim = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootTrim = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (pathTrim.Length == 0)
            return true;

        return string.Equals(pathTrim, rootTrim, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRoot(string root)
    {
        if (root == "/")
            return "/";

        if (root.Length >= 2 && root[1] == ':')
            return char.ToUpperInvariant(root[0]) + ":\\";

        if (root.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var trimmed = root.TrimEnd('\\');
            return trimmed.Length == 0 ? root : trimmed;
        }

        return root;
    }
}
