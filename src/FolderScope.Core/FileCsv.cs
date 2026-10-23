using System.Globalization;
using System.Text;

namespace FolderScope;

public static class FileCsv
{
    public const string Header = "Path,Name,SizeBytes,Size,DateField,DateUtc,DateLocal,ModifiedUtc,CreatedUtc,AccessedUtc";

    public static void Write(TextWriter writer, IEnumerable<FileHit> files)
    {
        writer.WriteLine(Header);
        foreach (var file in files)
        {
            writer.Write(Escape(file.FullPath));
            writer.Write(',');
            writer.Write(Escape(file.Name));
            writer.Write(',');
            writer.Write(file.Size.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(Escape(SizeFormatter.Format(file.Size)));
            writer.Write(',');
            writer.Write(Escape(DateText.FieldName(file.Field)));
            writer.Write(',');
            writer.Write(Escape(DateText.FormatUtcOrEmpty(file.SelectedUtc)));
            writer.Write(',');
            writer.Write(Escape(DateText.FormatLocalOrEmpty(file.SelectedUtc)));
            writer.Write(',');
            writer.Write(Escape(DateText.FormatUtcOrEmpty(file.ModifiedUtc)));
            writer.Write(',');
            writer.Write(Escape(DateText.FormatUtcOrEmpty(file.CreatedUtc)));
            writer.Write(',');
            writer.Write(Escape(DateText.FormatUtcOrEmpty(file.AccessedUtc)));
            writer.WriteLine();
        }
    }

    public static void WriteFile(string path, IReadOnlyList<FileHit> files)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Write(writer, files);
    }

    public static string Escape(string? value)
    {
        value ??= "";
        var needsQuotes = false;
        foreach (var ch in value)
        {
            if (ch is '"' or ',' or '\n' or '\r')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes)
            return value;

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
