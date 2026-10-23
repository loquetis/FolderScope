namespace FolderScope;

internal static class PanePreference
{
    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderScope", "panes.txt");

    public static bool LoadDual()
    {
        try
        {
            var text = File.ReadAllText(FilePath).Trim();
            if (string.Equals(text, "single", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(text, "dual", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
        }

        return true;
    }

    public static void SaveDual(bool dual)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, dual ? "dual" : "single");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
