namespace FolderScope;

internal static class FileTime
{
    public static DateTime NormalizeUtc(DateTime value)
    {
        if (value == default)
            return default;

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
