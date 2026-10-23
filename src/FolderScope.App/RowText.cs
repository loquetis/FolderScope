using System.Text;

namespace FolderScope;

internal static class RowText
{
    public static string FilesCell(ListingRow row) =>
        row.IsDirectory ? SizeFormatter.FormatCount(row.LiveFiles) : "—";

    public static string MatchFilesCell(ListingRow row) =>
        SizeFormatter.FormatCount(row.MatchFiles);

    public static string ModifiedCell(ListingRow row) =>
        DateText.FormatLocal(row.ModifiedUtc);

    public static string Tooltip(ListingRow row)
    {
        var text = new StringBuilder();
        text.AppendLine(row.FullPath);
        text.Append("Size: ").Append(SizeFormatter.Format(row.LiveSize));
        if (row.IsDirectory)
            text.Append("    Files: ").Append(SizeFormatter.FormatCount(row.LiveFiles));

        if (row.ModifiedUtc != default)
        {
            text.AppendLine();
            text.Append("Modified: ").Append(DateText.FormatLocal(row.ModifiedUtc));
        }

        if (row.MatchFiles > 0 || row.MatchBytes > 0)
        {
            text.AppendLine();
            text.Append("In the date range: ")
                .Append(SizeFormatter.Format(row.MatchBytes))
                .Append("    Files: ")
                .Append(SizeFormatter.FormatCount(row.MatchFiles));
        }

        if (row.IsReparsePoint)
        {
            text.AppendLine();
            text.Append("Shortcut or link");
            if (!string.IsNullOrEmpty(row.LinkTarget))
                text.Append(" → ").Append(row.LinkTarget);
            text.Append(". Not followed, so it is not counted twice.");
        }

        if (row.AccessDenied)
        {
            text.AppendLine();
            text.Append("Access denied.");
        }

        var extraDenied = row.DeniedCount - (row.AccessDenied ? 1 : 0);
        if (extraDenied > 0)
        {
            text.AppendLine();
            text.Append(SizeFormatter.FormatCount(extraDenied));
            text.Append(extraDenied == 1
                ? " folder inside could not be read."
                : " folders inside could not be read.");
        }

        if (!string.IsNullOrEmpty(row.Error) && !row.AccessDenied)
        {
            text.AppendLine();
            text.Append(row.Error);
        }

        return text.ToString().Trim();
    }
}
