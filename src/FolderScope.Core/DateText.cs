using System.Globalization;

namespace FolderScope;

public static class DateText
{
    public static string FormatLocal(DateTime utc) =>
        utc == default ? "—" : FormatLocalValue(utc);

    public static string FormatLocalOrEmpty(DateTime utc) =>
        utc == default ? "" : FormatLocalValue(utc);

    public static string FormatUtcOrEmpty(DateTime utc)
    {
        if (utc == default)
            return "";

        var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string FieldName(DateField field) =>
        field switch
        {
            DateField.Created => "Created",
            DateField.Accessed => "Last accessed",
            _ => "Last modified"
        };

    private static string FormatLocalValue(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Local ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
