namespace FolderScope;

public static class ConflictNames
{
    public static string Suffixed(string name, int index, bool directory)
    {
        if (index < 2)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (directory || string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(name)))
            return name + " (" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";

        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        return stem + " (" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")" + ext;
    }

    public static string NextAvailable(string directory, string name, bool directoryItem, Func<string, bool> exists)
    {
        if (!exists(TransferPaths.Join(directory, name)))
            return name;

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Suffixed(name, index, directoryItem);
            if (!exists(TransferPaths.Join(directory, candidate)))
                return candidate;
        }

        throw new IOException("Couldn't find a free name for " + name + ".");
    }
}
