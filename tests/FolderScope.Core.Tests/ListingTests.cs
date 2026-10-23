namespace FolderScope.Core.Tests;

public class ListingTests
{
    [Fact]
    public void Sort_and_filter_use_the_current_folder()
    {
        var root = PathHelper.NormalizeDirectory(Path.Combine(Path.GetTempPath(), "folderscope-list", Guid.NewGuid().ToString("n")));
        var fs = new FakeFileSystem();
        fs.AddDirectory(
            root,
            MakeFile("report.bin", 50),
            MakeFile("notes.txt", 10),
            MakeFile("archive.bin", 75),
            MakeDir("photos"));
        fs.AddDirectory(PathHelper.Combine(root, "photos"), MakeFile("img001.jpg", 20), MakeFile("img2.jpg", 5));

        var folder = new FolderScanner(fs).Scan(root).Root;
        var bySize = FolderListing.Build(folder, null, FolderColumn.Size, FolderSortDirection.Descending);
        Assert.Equal(["archive.bin", "report.bin", "photos", "notes.txt"], bySize.Select(row => row.Name).ToArray());

        var filtered = FolderListing.Build(folder, "  BIN ", FolderColumn.Name, FolderSortDirection.Ascending);
        Assert.Equal(["archive.bin", "report.bin"], filtered.Select(row => row.Name).ToArray());

        Assert.Equal(160, folder.ReadSize());
        Assert.Equal("46.9%", SizeFormatter.FormatPercent(bySize[0].SortSize, folder.ReadSize()));
        Assert.Equal("0%", SizeFormatter.FormatPercent(0, folder.ReadSize()));
    }

    [Theory]
    [InlineData("file2", "file10", -1)]
    [InlineData("file10", "file2", 1)]
    [InlineData("FileA", "filea", 0)]
    [InlineData("img001", "img1", 0)]
    [InlineData("a", "b", -1)]
    public void Natural_sort_orders_embedded_numbers(string left, string right, int expectedSign)
    {
        var actual = NaturalSort.Compare(left, right);
        Assert.Equal(Math.Sign(expectedSign), Math.Sign(actual));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(10 * 1024, "10.0 KB")]
    [InlineData(100 * 1024, "100 KB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5.00 GB")]
    public void Formats_binary_sizes(long bytes, string expected) =>
        Assert.Equal(expected, SizeFormatter.Format(bytes));

    [Fact]
    public void Formats_counts_and_duration()
    {
        Assert.Equal("1,234,567", SizeFormatter.FormatCount(1_234_567));
        Assert.Equal("1:05", SizeFormatter.FormatDuration(TimeSpan.FromSeconds(65)));
        Assert.Equal("1:02:03", SizeFormatter.FormatDuration(new TimeSpan(1, 2, 3)));
        Assert.Equal("1,500 files/s", SizeFormatter.FormatRate(1500));
        Assert.Equal("", SizeFormatter.FormatRate(0));
    }

    [Fact]
    public void Long_path_syntax_covers_drives_unc_and_existing_prefixes()
    {
        Assert.Equal(@"\\?\C:\Windows", PathHelper.ToLongPathSyntax(@"C:\Windows"));
        Assert.Equal(@"\\?\C:\", PathHelper.ToLongPathSyntax(@"C:\"));
        Assert.Equal(@"\\?\UNC\server\share\a", PathHelper.ToLongPathSyntax(@"\\server\share\a"));
        Assert.Equal(@"\\?\C:\Windows", PathHelper.ToLongPathSyntax(@"\\?\C:\Windows"));
        Assert.Equal(@"C:\Windows", PathHelper.StripLongPathPrefix(@"\\?\C:\Windows"));
        Assert.Equal(@"\\server\share\a", PathHelper.StripLongPathPrefix(@"\\?\UNC\server\share\a"));
    }

    [Fact]
    public void Normalize_strips_a_trailing_slash_and_keeps_the_root()
    {
        var file = Path.GetTempFileName();
        var directory = Path.Combine(Path.GetTempPath(), "folderscope-norm-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directory);
        try
        {
            var normalized = PathHelper.NormalizeDirectory(directory + Path.DirectorySeparatorChar);
            Assert.Equal(PathHelper.NormalizeDirectory(directory), normalized);
            Assert.False(normalized.EndsWith(Path.DirectorySeparatorChar));
            Assert.Equal(Path.GetFileName(directory), PathHelper.DisplayName(normalized));

            var root = Path.GetPathRoot(directory)!;
            Assert.Equal(root, PathHelper.NormalizeDirectory(root));
            Assert.False(string.IsNullOrEmpty(PathHelper.DisplayName(root)));
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static FsEntry MakeDir(string name) =>
        new(name, true, 0, FileAttributes.Directory, null);

    private static FsEntry MakeFile(string name, long size) =>
        new(name, false, size, FileAttributes.Normal, null);
}
