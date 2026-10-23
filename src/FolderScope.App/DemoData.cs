namespace FolderScope;

internal static class DemoData
{
    public static (string Left, string Right) Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "FolderScope-demo");
        TryDelete(root);
        var left = Path.Combine(root, "Archive");
        var right = Path.Combine(root, "Everyday");
        WriteArchive(left);
        WriteEveryday(right);
        return (left, right);
    }

    private static void WriteArchive(string root)
    {
        Directory.CreateDirectory(root);
        Write(root, "camera-notes.txt", 18_000, daysAgo: 12);
        Write(root, "budget-2026.xlsx", 340_000, daysAgo: 4);
        var video = Path.Combine(root, "Video projects");
        Directory.CreateDirectory(video);
        Write(video, "reel-2024.mov", 820L * 1024 * 1024, daysAgo: 40);
        Write(video, "interview.wav", 140L * 1024 * 1024, daysAgo: 38);
        Write(video, "titles.prproj", 48L * 1024 * 1024, daysAgo: 21);
        var client = Path.Combine(root, "Client deliverables");
        Directory.CreateDirectory(client);
        Write(client, "acme-final.zip", 260L * 1024 * 1024, daysAgo: 15);
        Write(client, "brand-guide.pdf", 18L * 1024 * 1024, daysAgo: 60);
        Write(client, "selects.zip", 90L * 1024 * 1024, daysAgo: 18);
        var installers = Path.Combine(root, "Installers");
        Directory.CreateDirectory(installers);
        Write(installers, "windows-sdk.iso", 1100L * 1024 * 1024, daysAgo: 200);
        Write(installers, "office-setup.exe", 420L * 1024 * 1024, daysAgo: 120);
        var old = Path.Combine(root, "Old backups");
        Directory.CreateDirectory(old);
        Write(old, "laptop-2019.vhdx", 640L * 1024 * 1024, daysAgo: 800);
        Write(old, "mail-archive.pst", 210L * 1024 * 1024, daysAgo: 500);
    }

    private static void WriteEveryday(string root)
    {
        Directory.CreateDirectory(root);
        Write(root, "inbox.mbox", 48L * 1024 * 1024, daysAgo: 2);
        Write(root, "readme.txt", 4_000, daysAgo: 1);
        var docs = Path.Combine(root, "Documents");
        Directory.CreateDirectory(docs);
        Write(docs, "taxes-2025.pdf", 6L * 1024 * 1024, daysAgo: 70);
        Write(docs, "lease.pdf", 2L * 1024 * 1024, daysAgo: 400);
        Write(docs, "resume.docx", 80_000, daysAgo: 25);
        var pictures = Path.Combine(root, "Pictures", "Vacation");
        Directory.CreateDirectory(pictures);
        for (var i = 1; i <= 6; i++)
            Write(pictures, "IMG_" + i.ToString("0000") + ".jpg", 4L * 1024 * 1024 + i * 50_000, daysAgo: 30);
        Write(pictures, "DSC_0102.nef", 28L * 1024 * 1024, daysAgo: 30);
        var music = Path.Combine(root, "Music");
        Directory.CreateDirectory(music);
        Write(music, "quartet-live.flac", 35L * 1024 * 1024, daysAgo: 90);
        Write(music, "practice.wav", 22L * 1024 * 1024, daysAgo: 9);
        var downloads = Path.Combine(root, "Downloads");
        Directory.CreateDirectory(downloads);
        Write(downloads, "dataset.csv", 150L * 1024 * 1024, daysAgo: 6);
        Write(downloads, "setup.exe", 88L * 1024 * 1024, daysAgo: 3);
    }

    private static void Write(string directory, string name, long bytes, int daysAgo)
    {
        var path = Path.Combine(directory, name);
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            stream.SetLength(bytes);
        var when = DateTime.UtcNow.AddDays(-daysAgo);
        File.SetLastWriteTimeUtc(path, when);
        File.SetCreationTimeUtc(path, when.AddDays(-2));
    }

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
