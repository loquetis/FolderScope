using System.Globalization;

namespace FolderScope;

public static class SizeFormatter
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        if (bytes < 0)
            bytes = 0;
        if (bytes < 1024)
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";

        double value = bytes;
        var unit = -1;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var number = value >= 100
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value >= 10
                ? value.ToString("0.0", CultureInfo.InvariantCulture)
                : value.ToString("0.00", CultureInfo.InvariantCulture);
        return number + " " + Units[unit];
    }

    public static string FormatPercent(long part, long whole)
    {
        if (part <= 0 || whole <= 0)
            return "0%";

        var percent = (double)part / whole * 100d;
        if (percent < 0.05)
            return "<0.1%";
        if (percent >= 99.95)
            return "100%";

        return percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    public static string FormatCount(long value)
    {
        if (value < 0)
            value = 0;
        return value.ToString("#,0", CultureInfo.InvariantCulture);
    }

    public static string FormatDuration(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed.TotalHours >= 1)
        {
            return ((int)elapsed.TotalHours).ToString(CultureInfo.InvariantCulture)
                + ":" + elapsed.Minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":" + elapsed.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        return elapsed.Minutes.ToString(CultureInfo.InvariantCulture)
            + ":" + elapsed.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    public static string FormatRate(double filesPerSecond)
    {
        if (filesPerSecond < 1 || double.IsNaN(filesPerSecond) || double.IsInfinity(filesPerSecond))
            return "";

        return FormatCount((long)Math.Round(filesPerSecond)) + " files/s";
    }
}
