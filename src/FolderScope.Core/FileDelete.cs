namespace FolderScope;

public static class FileDelete
{
    public static TransferResult Execute(
        IReadOnlyList<string> paths,
        ITransferIo io,
        ITransferCallbacks callbacks,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new TransferResult();
        long bytesTotal = 0;
        var filesTotal = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!io.TryDescribe(path, out var entry, out _))
            {
                result.Errors.Add(path + ": Couldn't find that item.");
                continue;
            }

            try
            {
                Measure(io, path, entry.IsDirectory, entry.IsReparse, ref bytesTotal, ref filesTotal);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                result.Errors.Add(path + ": " + FileTransfer.Describe(ex));
            }
        }

        var state = new State(bytesTotal, filesTotal);
        try
        {
            foreach (var path in paths)
            {
                if (cancellationToken.IsCancellationRequested || state.Stop)
                    break;

                if (!io.TryDescribe(path, out var entry, out _))
                    continue;

                var removed = new List<TransferChange>();
                DeleteNode(path, entry.IsDirectory, entry.IsReparse, io, callbacks, progress, state, result, removed, cancellationToken);
                result.Changes.AddRange(removed);
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

    private static bool DeleteNode(
        string path,
        bool isDirectory,
        bool isReparse,
        ITransferIo io,
        ITransferCallbacks callbacks,
        IProgress<TransferProgress>? progress,
        State state,
        TransferResult result,
        List<TransferChange> removed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isReparse || !isDirectory)
        {
            Report(progress, state, path);
            long length = 0;
            if (!isDirectory && !isReparse && io.TryDescribe(path, out var described, out _))
                length = Math.Max(0, described.Length);

            try
            {
                if (isDirectory)
                    io.DeleteDirectoryEntry(path);
                else
                    io.DeleteFile(path);

                if (!isDirectory)
                {
                    state.FilesDone++;
                    state.BytesDone += length;
                }

                Report(progress, state, path);
                removed.Add(new TransferChange { Source = path, SourceRemoved = true, IsDirectory = isDirectory });
                return true;
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                if (!Ask(path, ex, callbacks, state, result))
                    state.Stop = true;
                return false;
            }
        }

        var children = new List<TransferEntry>();
        try
        {
            children.AddRange(io.Enumerate(path));
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            if (!Ask(path, ex, callbacks, state, result))
                state.Stop = true;
            return false;
        }

        var childRemoved = new List<TransferChange>();
        var complete = true;
        foreach (var child in children)
        {
            if (cancellationToken.IsCancellationRequested || state.Stop)
            {
                complete = false;
                break;
            }

            var childPath = TransferPaths.Join(path, child.Name);
            if (!DeleteNode(childPath, child.IsDirectory, child.IsReparse, io, callbacks, progress, state, result, childRemoved, cancellationToken))
                complete = false;
        }

        if (complete && !cancellationToken.IsCancellationRequested && !state.Stop)
        {
            try
            {
                io.DeleteDirectoryEntry(path);
                removed.Add(new TransferChange { Source = path, SourceRemoved = true, IsDirectory = true });
                return true;
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                if (!Ask(path, ex, callbacks, state, result))
                    state.Stop = true;
            }
        }

        removed.AddRange(childRemoved);
        return false;
    }

    private static void Measure(ITransferIo io, string path, bool isDirectory, bool isReparse, ref long bytes, ref int files)
    {
        if (isReparse || !isDirectory)
        {
            if (!isDirectory)
            {
                files++;
                if (!isReparse && io.TryDescribe(path, out var entry, out _))
                    bytes += Math.Max(0, entry.Length);
            }

            return;
        }

        foreach (var child in io.Enumerate(path))
            Measure(io, TransferPaths.Join(path, child.Name), child.IsDirectory, child.IsReparse, ref bytes, ref files);
    }

    private static bool Ask(string path, Exception ex, ITransferCallbacks callbacks, State state, TransferResult result)
    {
        var message = FileTransfer.Describe(ex);
        result.Errors.Add(path + ": " + message);
        if (state.ContinueAll)
            return true;

        var answer = callbacks.OnError(path, message);
        if (answer.ApplyToAll && answer.Continue)
            state.ContinueAll = true;
        return answer.Continue;
    }

    private static void Report(IProgress<TransferProgress>? progress, State state, string current) =>
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

    private sealed class State
    {
        public State(long bytesTotal, int filesTotal)
        {
            BytesTotal = bytesTotal;
            FilesTotal = filesTotal;
        }

        public long BytesTotal { get; }

        public int FilesTotal { get; }

        public long BytesDone { get; set; }

        public int FilesDone { get; set; }

        public bool Stop { get; set; }

        public bool ContinueAll { get; set; }
    }
}
