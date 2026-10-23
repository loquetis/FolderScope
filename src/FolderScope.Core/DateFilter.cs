namespace FolderScope;

public enum DateField
{
    Modified,
    Created,
    Accessed
}

public enum DateMatchKind
{
    Any,
    OlderThan,
    NewerThan,
    Between
}

/// <summary>
/// A date filter applied to timestamps already stored on the scan.
/// Older-than is exclusive of the cutoff. Newer-than includes the cutoff.
/// A range includes the start and excludes the end.
/// A default timestamp never matches an active filter.
/// </summary>
public readonly record struct DateFilter(
    DateField Field,
    DateMatchKind Kind,
    DateTime? StartUtc,
    DateTime? EndUtc)
{
    public static DateFilter Any(DateField field = DateField.Modified) =>
        new(field, DateMatchKind.Any, null, null);

    public bool IsActive => Kind != DateMatchKind.Any;

    public DateTime Select(DateTime modifiedUtc, DateTime createdUtc, DateTime accessedUtc) =>
        Field switch
        {
            DateField.Created => createdUtc,
            DateField.Accessed => accessedUtc,
            _ => modifiedUtc
        };

    public bool Matches(DateTime modifiedUtc, DateTime createdUtc, DateTime accessedUtc)
    {
        if (Kind == DateMatchKind.Any)
            return true;

        var stamp = Select(modifiedUtc, createdUtc, accessedUtc);
        if (stamp == default)
            return false;

        return Kind switch
        {
            DateMatchKind.OlderThan => EndUtc is { } end && stamp < end,
            DateMatchKind.NewerThan => StartUtc is { } start && stamp >= start,
            DateMatchKind.Between => StartUtc is { } start && EndUtc is { } end && stamp >= start && stamp < end,
            _ => true
        };
    }
}

public static class DatePresets
{
    public static DateFilter OlderThanDays(DateField field, int days, DateTime utcNow) =>
        new(field, DateMatchKind.OlderThan, null, utcNow.AddDays(-days));

    public static DateFilter OlderThanMonths(DateField field, int months, DateTime utcNow) =>
        new(field, DateMatchKind.OlderThan, null, utcNow.AddMonths(-months));

    public static DateFilter OlderThanYears(DateField field, int years, DateTime utcNow) =>
        new(field, DateMatchKind.OlderThan, null, utcNow.AddYears(-years));

    public static DateTime LocalDateStartUtc(DateTime localDate)
    {
        var day = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        try
        {
            if (TimeZoneInfo.Local.IsInvalidTime(day))
                day = day.AddHours(1);

            return TimeZoneInfo.ConvertTimeToUtc(day, TimeZoneInfo.Local);
        }
        catch (ArgumentException)
        {
            return DateTime.SpecifyKind(localDate.Date, DateTimeKind.Local).ToUniversalTime();
        }
    }

    public static DateFilter OlderThanLocalDate(DateField field, DateTime localDate) =>
        new(field, DateMatchKind.OlderThan, null, LocalDateStartUtc(localDate));

    public static DateFilter NewerThanLocalDate(DateField field, DateTime localDate) =>
        new(field, DateMatchKind.NewerThan, LocalDateStartUtc(localDate), null);

    public static DateFilter BetweenLocalDates(DateField field, DateTime startLocal, DateTime endLocal)
    {
        var startDay = startLocal.Date;
        var endDay = endLocal.Date;
        if (endDay < startDay)
            (startDay, endDay) = (endDay, startDay);

        var start = LocalDateStartUtc(startDay);
        var endExclusive = LocalDateStartUtc(endDay.AddDays(1));
        return new DateFilter(field, DateMatchKind.Between, start, endExclusive);
    }
}
