namespace FolderScope;

public enum TransferKind
{
    Copy,
    Move
}

public enum NameConflict
{
    Overwrite,
    Skip,
    Rename,
    Cancel
}

public enum LinkPolicy
{
    CopyLink,
    Skip
}

public readonly record struct ConflictResolution(NameConflict Choice, bool ApplyToAll);

public readonly record struct LinkResolution(LinkPolicy Choice, bool ApplyToAll);

public readonly record struct ErrorResolution(bool Continue, bool ApplyToAll);

public interface ITransferCallbacks
{
    ConflictResolution OnConflict(string source, string destination, bool isDirectory);

    LinkResolution OnReparse(string source, bool isDirectory);

    ErrorResolution OnError(string path, string message);
}

public sealed class TransferRequest
{
    public TransferKind Kind { get; init; }

    public IReadOnlyList<string> Sources { get; init; } = [];

    public string DestinationDirectory { get; init; } = "";

    public bool SameVolume { get; init; }
}

public sealed class TransferProgress
{
    public long BytesDone { get; init; }

    public long BytesTotal { get; init; }

    public int FilesDone { get; init; }

    public int FilesTotal { get; init; }

    public string Current { get; init; } = "";
}

public sealed class TransferChange
{
    public string Source { get; init; } = "";

    public string? Destination { get; init; }

    public bool SourceRemoved { get; init; }

    public bool Created { get; init; }

    public bool IsDirectory { get; init; }

    public long Bytes { get; init; }

    public TransferTimestamps Timestamps { get; init; }
}

public sealed class TransferResult
{
    public bool Cancelled { get; set; }

    public bool Stopped { get; set; }

    public List<TransferChange> Changes { get; } = [];

    public List<string> Errors { get; } = [];
}

public sealed class FileTransfer
{
    public TransferResult Execute(
        TransferRequest request,
        ITransferIo io,
        ITransferCallbacks callbacks,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new TransferResult();
        if (string.IsNullOrWhiteSpace(request.DestinationDirectory))
        {
            result.Errors.Add("Choose a destination folder.");
            result.Stopped = true;
            return result;
        }

        if (!io.DirectoryExists(request.DestinationDirectory))
        {
            result.Errors.Add("The destination folder does not exist.");
            result.Stopped = true;
            return result;
        }

        var plan = new List<Planned>();
        foreach (var source in request.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!io.TryDescribe(source, out var entry, out _))
            {
                result.Errors.Add(source + ": Couldn't find that item.");
                continue;
            }

            var blocked = BlockedReason(source, entry.IsDirectory, request.DestinationDirectory);
            if (blocked != null)
            {
                result.Errors.Add(source + ": " + blocked);
                result.Stopped = true;
                return result;
            }

            plan.Add(new Planned(source, entry.IsDirectory, entry.IsReparse));
        }

        long bytesTotal = 0;
        var filesTotal = 0;
        foreach (var item in plan)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Cancelled = true;
                return result;
            }

            try
            {
                Measure(io, item.Source, item.IsDirectory, item.IsReparse, ref bytesTotal, ref filesTotal);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                result.Errors.Add(item.Source + ": " + Describe(ex));
            }
        }

        var state = new RunState(bytesTotal, filesTotal);
        try
        {
            foreach (var item in plan)
            {
                if (cancellationToken.IsCancellationRequested || state.Stop)
                    break;

                TransferOne(item.Source, request.DestinationDirectory, item.IsDirectory, item.IsReparse, request, io, callbacks, progress, state, result, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            return result;
        }

        result.Cancelled = cancellationToken.IsCancellationRequested;
        result.Stopped = state.Stop && !result.Cancelled;
        return result;
    }

    public static string? BlockedReason(string source, bool isDirectory, string destinationDirectory)
    {
        if (TransferPaths.SamePath(source, destinationDirectory))
            return "That folder can't be copied into itself.";

        if (isDirectory && TransferPaths.IsSameOrInside(source, destinationDirectory))
            return "The destination is inside the folder being copied.";

        return null;
    }

    private static void TransferOne(
        string source,
        string destinationDirectory,
        bool isDirectory,
        bool isReparse,
        TransferRequest request,
        ITransferIo io,
        ITransferCallbacks callbacks,
        IProgress<TransferProgress>? progress,
        RunState state,
        TransferResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            if (isReparse)
            {
                TransferLink(source, destinationDirectory, isDirectory, request, io, callbacks, progress, state, result, cancellationToken);
                return;
            }

            if (!isDirectory)
            {
                TransferFile(source, destinationDirectory, request, io, callbacks, progress, state, result, cancellationToken);
                return;
            }

            var renameWhole = request.Kind == TransferKind.Move && request.SameVolume;
            var destName = ResolveName(source, destinationDirectory, isDirectory: true, io, callbacks, state, result);
            if (destName == null)
                return;

            var destination = TransferPaths.Join(destinationDirectory, destName);
            var merging = io.DirectoryExists(destination);
            if (renameWhole && !merging)
            {
                Report(progress, state, source);
                io.MovePath(source, destination, isDirectory: true);
                result.Changes.Add(new TransferChange
                {
                    Source = source,
                    Destination = destination,
                    SourceRemoved = true,
                    Created = true,
                    IsDirectory = true
                });
                return;
            }

            if (!merging)
                io.CreateDirectory(destination);

            var failed = false;
            foreach (var child in io.Enumerate(source))
            {
                if (cancellationToken.IsCancellationRequested || state.Stop)
                    break;

                var childPath = TransferPaths.Join(source, child.Name);
                var before = result.Errors.Count;
                TransferOne(childPath, destination, child.IsDirectory, child.IsReparse, request, io, callbacks, progress, state, result, cancellationToken);
                if (result.Errors.Count > before)
                    failed = true;
            }

            try
            {
                io.CopyMetadata(source, destination, isDirectory: true);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                // The files are in place. A timestamp miss should not fail the folder.
            }

            var removeSource = request.Kind == TransferKind.Move && !failed && !cancellationToken.IsCancellationRequested && !state.Stop;
            if (removeSource)
                DeleteIfEmpty(io, source);

            result.Changes.Add(new TransferChange
            {
                Source = source,
                Destination = destination,
                SourceRemoved = removeSource && !io.DirectoryExists(source),
                Created = !merging,
                IsDirectory = true
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            if (!AskContinue(source, ex, callbacks, state, result))
                state.Stop = true;
        }
    }

    private static void TransferFile(
        string source,
        string destinationDirectory,
        TransferRequest request,
        ITransferIo io,
        ITransferCallbacks callbacks,
        IProgress<TransferProgress>? progress,
        RunState state,
        TransferResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destName = ResolveName(source, destinationDirectory, isDirectory: false, io, callbacks, state, result);
        if (destName == null)
            return;

        var destination = TransferPaths.Join(destinationDirectory, destName);
        if (TransferPaths.SamePath(source, destination))
        {
            result.Changes.Add(new TransferChange { Source = source });
            return;
        }

        Report(progress, state, source);
        var length = DescribedLength(io, source);
        var moveDirect = request.Kind == TransferKind.Move && request.SameVolume && !io.FileExists(destination);
        if (moveDirect)
        {
            io.MovePath(source, destination, isDirectory: false);
            state.BytesDone += length;
            state.FilesDone++;
            Report(progress, state, source);
            result.Changes.Add(FileChange(source, destination, removed: true, length, io));
            return;
        }

        if (io.FileExists(destination))
            io.ClearReadOnly(destination);

        io.CopyFile(source, destination, bytes =>
        {
            state.BytesDone += bytes;
            Report(progress, state, source);
        }, cancellationToken);

        try
        {
            io.CopyMetadata(source, destination, isDirectory: false);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
        }

        var copied = DescribedLength(io, destination);
        if (copied != length)
            throw new IOException("The copy of " + NameOf(source) + " doesn't match the original, so the original was kept.");

        state.FilesDone++;
        Report(progress, state, source);
        var removed = false;
        if (request.Kind == TransferKind.Move)
        {
            io.DeleteFile(source);
            removed = true;
        }

        result.Changes.Add(FileChange(source, destination, removed, length, io));
    }

    private static void TransferLink(
        string source,
        string destinationDirectory,
        bool isDirectory,
        TransferRequest request,
        ITransferIo io,
        ITransferCallbacks callbacks,
        IProgress<TransferProgress>? progress,
        RunState state,
        TransferResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.LinkApplyAll.HasValue)
        {
            var answer = callbacks.OnReparse(source, isDirectory);
            if (answer.ApplyToAll)
                state.LinkApplyAll = answer.Choice;
            state.LinkChoice = answer.Choice;
        }

        if ((state.LinkApplyAll ?? state.LinkChoice) == LinkPolicy.Skip)
        {
            result.Changes.Add(new TransferChange { Source = source, IsDirectory = isDirectory });
            return;
        }

        if (!io.TryDescribe(source, out _, out var target) || string.IsNullOrEmpty(target))
            throw new IOException("This link has no target, so its contents were not copied.");

        var destName = ResolveName(source, destinationDirectory, isDirectory, io, callbacks, state, result);
        if (destName == null)
            return;

        var destination = TransferPaths.Join(destinationDirectory, destName);
        Report(progress, state, source);
        io.CopyLink(target, destination, isDirectory);
        if (!isDirectory)
        {
            state.FilesDone++;
            Report(progress, state, source);
        }

        var removed = false;
        if (request.Kind == TransferKind.Move)
        {
            if (isDirectory)
                io.DeleteDirectoryEntry(source);
            else
                io.DeleteFile(source);
            removed = true;
        }

        result.Changes.Add(new TransferChange
        {
            Source = source,
            Destination = destination,
            SourceRemoved = removed,
            Created = true,
            IsDirectory = isDirectory
        });
    }

    private static string? ResolveName(
        string source,
        string destinationDirectory,
        bool isDirectory,
        ITransferIo io,
        ITransferCallbacks callbacks,
        RunState state,
        TransferResult result)
    {
        var name = NameOf(source);
        var destination = TransferPaths.Join(destinationDirectory, name);
        if (!Exists(io, destination))
            return name;

        NameConflict choice;
        if (state.ConflictApplyAll is { } saved)
        {
            choice = saved;
        }
        else
        {
            var answer = callbacks.OnConflict(source, destination, isDirectory);
            choice = answer.Choice;
            if (answer.ApplyToAll && choice != NameConflict.Cancel)
                state.ConflictApplyAll = choice;
        }

        switch (choice)
        {
            case NameConflict.Skip:
                result.Changes.Add(new TransferChange { Source = source, IsDirectory = isDirectory });
                return null;
            case NameConflict.Cancel:
                state.Stop = true;
                return null;
            case NameConflict.Rename:
                return ConflictNames.NextAvailable(destinationDirectory, name, isDirectory, path => Exists(io, path));
            default:
                return name;
        }
    }

    private static void DeleteIfEmpty(ITransferIo io, string path)
    {
        if (!io.DirectoryExists(path))
            return;

        if (io.TryDescribe(path, out var self, out _) && self.IsReparse)
            return;

        var any = false;
        foreach (var _ in io.Enumerate(path))
        {
            any = true;
            break;
        }

        if (!any)
            io.DeleteDirectoryEntry(path);
    }

    private static void Measure(ITransferIo io, string path, bool isDirectory, bool isReparse, ref long bytes, ref int files)
    {
        if (isReparse || !isDirectory)
        {
            if (!isDirectory && !isReparse)
            {
                bytes += DescribedLength(io, path);
                files++;
            }
            else if (!isDirectory)
            {
                files++;
            }

            return;
        }

        foreach (var child in io.Enumerate(path))
            Measure(io, TransferPaths.Join(path, child.Name), child.IsDirectory, child.IsReparse, ref bytes, ref files);
    }

    private static long DescribedLength(ITransferIo io, string path)
    {
        if (io.TryDescribe(path, out var entry, out _))
            return Math.Max(0, entry.Length);
        return 0;
    }

    private static TransferChange FileChange(string source, string destination, bool removed, long length, ITransferIo io)
    {
        TransferTimestamps stamps;
        try
        {
            stamps = io.GetTimestamps(destination);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            stamps = default;
        }

        return new TransferChange
        {
            Source = source,
            Destination = destination,
            SourceRemoved = removed,
            Created = true,
            IsDirectory = false,
            Bytes = length,
            Timestamps = stamps
        };
    }

    private static bool AskContinue(string path, Exception ex, ITransferCallbacks callbacks, RunState state, TransferResult result)
    {
        var message = Describe(ex);
        result.Errors.Add(path + ": " + message);
        if (state.ContinueAllErrors)
            return true;

        var answer = callbacks.OnError(path, message);
        if (answer.ApplyToAll && answer.Continue)
            state.ContinueAllErrors = true;
        return answer.Continue;
    }

    public static string Describe(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
            return "Access denied.";

        if (ex is IOException io)
        {
            var code = io.HResult & 0xFFFF;
            if (code is 32 or 33)
                return "The file is in use.";
            if (code == 5)
                return "Access denied.";
            if (!string.IsNullOrWhiteSpace(io.Message))
                return io.Message.Trim();
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? "The operation failed." : ex.Message.Trim();
    }

    private static bool Exists(ITransferIo io, string path) =>
        io.FileExists(path) || io.DirectoryExists(path);

    private static void Report(IProgress<TransferProgress>? progress, RunState state, string current) =>
        progress?.Report(new TransferProgress
        {
            BytesDone = state.BytesDone,
            BytesTotal = state.BytesTotal,
            FilesDone = state.FilesDone,
            FilesTotal = state.FilesTotal,
            Current = current
        });

    private static bool IsExpected(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static string NameOf(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var sep = TransferPaths.Separator(trimmed);
        var index = trimmed.LastIndexOf(sep);
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    private static string? ParentOf(string path)
    {
        var trimmed = TransferPaths.Normalize(path);
        var sep = TransferPaths.Separator(trimmed);
        var index = trimmed.LastIndexOf(sep);
        if (index <= 0)
            return null;
        return trimmed[..index];
    }

    private sealed class Planned
    {
        public Planned(string source, bool isDirectory, bool isReparse)
        {
            Source = source;
            IsDirectory = isDirectory;
            IsReparse = isReparse;
        }

        public string Source { get; }

        public bool IsDirectory { get; }

        public bool IsReparse { get; }
    }

    private sealed class RunState
    {
        public RunState(long bytesTotal, int filesTotal)
        {
            BytesTotal = bytesTotal;
            FilesTotal = filesTotal;
        }

        public long BytesTotal { get; }

        public int FilesTotal { get; }

        public long BytesDone { get; set; }

        public int FilesDone { get; set; }

        public bool Stop { get; set; }

        public bool ContinueAllErrors { get; set; }

        public NameConflict? ConflictApplyAll { get; set; }

        public LinkPolicy? LinkApplyAll { get; set; }

        public LinkPolicy LinkChoice { get; set; } = LinkPolicy.Skip;
    }
}
