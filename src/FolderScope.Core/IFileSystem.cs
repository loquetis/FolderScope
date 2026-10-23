namespace FolderScope;

public interface IFileSystem
{
    bool FileExists(string path);

    IEnumerable<FsEntry> Enumerate(string path);
}
