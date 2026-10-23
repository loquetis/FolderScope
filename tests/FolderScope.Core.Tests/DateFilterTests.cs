namespace FolderScope.Core.Tests;

public class DateFilterTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    public void Older_than_a_day_preset_excludes_the_cutoff(int days)
    {
        var filter = DatePresets.OlderThanDays(DateField.Modified, days, Now);
        var cutoff = Now.AddDays(-days);

        Assert.Equal(cutoff, filter.EndUtc);
        Assert.False(filter.Matches(cutoff, default, default));
        Assert.True(filter.Matches(cutoff.AddTicks(-1), default, default));
        Assert.False(filter.Matches(default, default, default));
    }

    [Fact]
    public void Month_and_year_presets_use_calendar_steps()
    {
        Assert.Equal(Now.AddMonths(-6), DatePresets.OlderThanMonths(DateField.Modified, 6, Now).EndUtc);
        Assert.Equal(Now.AddYears(-1), DatePresets.OlderThanYears(DateField.Modified, 1, Now).EndUtc);
        Assert.Equal(Now.AddYears(-2), DatePresets.OlderThanYears(DateField.Modified, 2, Now).EndUtc);
        Assert.Equal(Now.AddYears(-5), DatePresets.OlderThanYears(DateField.Modified, 5, Now).EndUtc);

        var fiveYears = DatePresets.OlderThanYears(DateField.Modified, 5, Now);
        Assert.True(fiveYears.Matches(new DateTime(2021, 10, 18, 11, 59, 59, DateTimeKind.Utc), default, default));
        Assert.False(fiveYears.Matches(new DateTime(2021, 10, 18, 12, 0, 0, DateTimeKind.Utc), default, default));
    }

    [Fact]
    public void Date_field_selects_which_timestamp_is_compared()
    {
        var modified = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var accessed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var older = DatePresets.OlderThanYears(DateField.Modified, 5, Now);

        Assert.False(older.Matches(modified, created, accessed));
        Assert.True((older with { Field = DateField.Created }).Matches(modified, created, accessed));
        Assert.False((older with { Field = DateField.Accessed }).Matches(modified, created, accessed));
        Assert.Equal(created, (older with { Field = DateField.Created }).Select(modified, created, accessed));
    }

    [Fact]
    public void Custom_dates_use_local_calendar_days()
    {
        var day = new DateTime(2020, 1, 15);
        var start = DatePresets.LocalDateStartUtc(day);
        var older = DatePresets.OlderThanLocalDate(DateField.Modified, day);
        var newer = DatePresets.NewerThanLocalDate(DateField.Created, day);

        Assert.True(older.Matches(start.AddTicks(-1), default, default));
        Assert.False(older.Matches(start, default, default));
        Assert.True(newer.Matches(default, start, default));
        Assert.False(newer.Matches(default, start.AddTicks(-1), default));

        var range = DatePresets.BetweenLocalDates(DateField.Accessed, day, day.AddDays(1));
        var next = DatePresets.LocalDateStartUtc(day.AddDays(1));
        var after = DatePresets.LocalDateStartUtc(day.AddDays(2));
        Assert.True(range.Matches(default, default, start));
        Assert.True(range.Matches(default, default, next.AddHours(12)));
        Assert.False(range.Matches(default, default, after));

        var swapped = DatePresets.BetweenLocalDates(DateField.Modified, day.AddDays(3), day);
        Assert.Equal(start, swapped.StartUtc);
        Assert.Equal(DatePresets.LocalDateStartUtc(day.AddDays(4)), swapped.EndUtc);
    }

    [Fact]
    public void Scan_keeps_timestamps_and_match_totals_without_a_rescan()
    {
        var old = new DateTime(2010, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var root = NewRoot();
        var sub = PathHelper.Combine(root, "sub");
        var link = PathHelper.Combine(root, "link");
        var fs = new FakeFileSystem();
        fs.AddDirectory(
            root,
            DatedFile("recent.bin", 10, recent, recent, recent),
            DatedFile("old.bin", 40, old, old, recent),
            Dir("sub", old),
            new FsEntry("link", true, 0, FileAttributes.Directory | FileAttributes.ReparsePoint, sub, old, old, old));
        fs.AddDirectory(sub, DatedFile("old2.bin", 25, old, old, recent), DatedFile("new.bin", 5, recent, recent, recent));
        fs.AddDirectory(link, DatedFile("hidden.bin", 999, old));

        var result = new FolderScanner(fs, degreeOfParallelism: 4).Scan(root);
        Assert.Equal(80, result.Root.ReadSize());

        var filter = DatePresets.OlderThanYears(DateField.Modified, 1, Now);
        var rows = FolderListing.Build(result.Root, null, FolderColumn.MatchSize, FolderSortDirection.Descending, filter, out var match);

        Assert.Equal(65, match.Bytes);
        Assert.Equal(2, match.Files);
        Assert.Equal(80, result.Root.ReadSize());
        Assert.Equal(["old.bin", "sub", "link", "recent.bin"], rows.Select(row => row.Name).ToArray());
        Assert.Equal(40, rows[0].MatchBytes);
        Assert.Equal(1, rows[0].MatchFiles);
        Assert.Equal(25, rows[1].MatchBytes);
        Assert.Equal(0, rows[2].MatchBytes);
        Assert.Equal(old, rows[1].ModifiedUtc);

        var byModified = FolderListing.Build(result.Root, null, FolderColumn.Modified, FolderSortDirection.Descending, filter);
        Assert.Equal("recent.bin", byModified[0].Name);
        Assert.Equal(recent, byModified[0].ModifiedUtc);

        var created = filter with { Field = DateField.Created };
        var createdMatch = DateRollup.Compute(result.Root, created)[result.Root];
        Assert.Equal(65, createdMatch.Bytes);

        var accessed = DatePresets.OlderThanDays(DateField.Accessed, 30, Now);
        Assert.Equal(0, DateRollup.Compute(result.Root, accessed)[result.Root].Files);

        var files = FileListing.Collect(result.Root, filter, null, FileColumn.Size, FolderSortDirection.Descending);
        Assert.Equal(["old.bin", "old2.bin"], files.Files.Select(file => file.Name).ToArray());
        Assert.Equal(65, files.MatchBytes);
        Assert.Equal(2, files.MatchFiles);

        var named = FileListing.Collect(result.Root, filter, "old2", FileColumn.Name, FolderSortDirection.Ascending);
        Assert.Equal(["old2.bin"], named.Files.Select(file => file.Name).ToArray());
        Assert.Equal(2, named.MatchFiles);

        var direct = FileListing.Collect(result.Root, DateFilter.Any(), null, FileColumn.Name, FolderSortDirection.Ascending);
        Assert.Equal(["old.bin", "recent.bin"], direct.Files.Select(file => file.Name).ToArray());
        Assert.Equal(0, direct.MatchFiles);
    }

    [Fact]
    public void File_links_keep_timestamps_and_export_escapes_csv()
    {
        var old = new DateTime(2012, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var root = NewRoot();
        var fs = new FakeFileSystem();
        fs.AddDirectory(
            root,
            DatedFile("report, \"final\".bin", 12, old),
            new FsEntry("alias.bin", false, 12, FileAttributes.ReparsePoint, "report, \"final\".bin", old, old, old));

        var scanned = new FolderScanner(fs).Scan(root);
        Assert.Equal(12, scanned.Root.ReadSize());

        var filter = DatePresets.OlderThanYears(DateField.Modified, 5, Now);
        var files = FileListing.Collect(scanned.Root, filter, null, FileColumn.Path, FolderSortDirection.Ascending);
        Assert.Equal(2, files.Files.Length);
        var link = Assert.Single(files.Files, file => file.Name == "alias.bin");
        Assert.True(link.IsReparsePoint);
        Assert.Equal(0, link.Size);
        Assert.Equal(old, link.ModifiedUtc);

        var writer = new StringWriter();
        FileCsv.Write(writer, files.Files);
        var csv = writer.ToString();
        Assert.StartsWith(FileCsv.Header, csv);
        Assert.Contains("\"report, \"\"final\"\".bin\"", csv);
        Assert.Contains("2012-06-01 00:00:00", csv);
        Assert.Contains("Last modified", csv);
    }

    [Fact]
    public void System_scan_reads_last_write_time()
    {
        using var tree = new TempTree();
        var folder = Path.Combine(tree.Root, "old");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "note.txt");
        File.WriteAllBytes(file, new byte[32]);
        var write = new DateTime(2018, 4, 5, 6, 7, 8, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, write);
        Directory.SetLastWriteTimeUtc(folder, write);

        var result = new FolderScanner().Scan(tree.Root);
        var rows = FolderListing.Build(result.Root, null, FolderColumn.Name, FolderSortDirection.Ascending);
        var folderRow = Assert.Single(rows, row => row.Name == "old");
        Assert.Equal(File.GetLastWriteTimeUtc(folder), folderRow.ModifiedUtc);

        var hits = FileListing.Collect(
            result.Root,
            DatePresets.OlderThanYears(DateField.Modified, 1, DateTime.UtcNow),
            null,
            FileColumn.Name,
            FolderSortDirection.Ascending);
        var hit = Assert.Single(hits.Files);
        Assert.Equal(File.GetLastWriteTimeUtc(file), hit.ModifiedUtc);
        Assert.Equal(32, hit.Size);
        Assert.NotEqual(default, hit.ModifiedUtc);
    }

    private static string NewRoot() =>
        PathHelper.NormalizeDirectory(Path.Combine(Path.GetTempPath(), "folderscope-dates", Guid.NewGuid().ToString("n")));

    private static FsEntry DatedFile(string name, long size, DateTime write, DateTime created = default, DateTime accessed = default) =>
        new(name, false, size, FileAttributes.Normal, null, write, created == default ? write : created, accessed == default ? write : accessed);

    private static FsEntry Dir(string name, DateTime write) =>
        new(name, true, 0, FileAttributes.Directory, null, write, write, write);
}
