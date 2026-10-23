using System.Collections.Concurrent;

namespace FolderScope;

public sealed class FolderScanner
{
    public const int FileBatchSize = 64;
    public const int DirectoryBatchSize = 16;

    private readonly IFileSystem _fileSystem;
    private readonly int _workers;

    public FolderScanner(IFileSystem? fileSystem = null, int? degreeOfParallelism = null)
    {
        _fileSystem = fileSystem ?? new SystemFileSystem();
        var requested = degreeOfParallelism ?? Math.Min(8, Math.Max(1, Environment.ProcessorCount));
        _workers = Math.Clamp(requested, 1, 32);
    }

    public ScanResult Scan(
        string path,
        ScanProgress? progress = null,
        CancellationToken cancellationToken = default,
        Action<FolderNode>? onRootCreated = null)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        progress ??= new ScanProgress();

        FolderNode root;
        try
        {
            var display = PathHelper.NormalizeDirectory(path);
            root = new FolderNode(PathHelper.DisplayName(display), display, parent: null);
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            root = new FolderNode(SafeName(path), SafeName(path), parent: null);
            root.Fail(TrimMessage(ex.Message));
            onRootCreated?.Invoke(root);
            watch.Stop();
            return new ScanResult(root, cancelled: false, watch.Elapsed, progress);
        }

        onRootCreated?.Invoke(root);

        try
        {
            if (_fileSystem.FileExists(root.FullPath))
            {
                root.Fail("That path is a file, not a folder.");
                watch.Stop();
                return new ScanResult(root, cancelled: false, watch.Elapsed, progress);
            }
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            // The probe failed. Enumeration reports the real problem.
        }

        var queue = new BlockingCollection<FolderNode>(new ConcurrentQueue<FolderNode>());
        var state = new WorkState(queue);
        state.Enqueue(root);

        var tasks = new Task[_workers];
        for (var i = 0; i < _workers; i++)
        {
            tasks[i] = Task.Factory.StartNew(
                () => Worker(queue, state, progress, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        try
        {
            Task.WaitAll(tasks);
        }
        catch (AggregateException ex)
        {
            var unexpected = ex.Flatten().InnerExceptions.Where(e => e is not OperationCanceledException).ToArray();
            if (unexpected.Length > 0)
                throw new AggregateException(unexpected);
        }

        watch.Stop();
        return new ScanResult(root, cancellationToken.IsCancellationRequested, watch.Elapsed, progress);
    }

    private void Worker(
        BlockingCollection<FolderNode> queue,
        WorkState state,
        ScanProgress progress,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var node in queue.GetConsumingEnumerable(cancellationToken))
            {
                try
                {
                    Visit(node, progress, cancellationToken, state);
                }
                finally
                {
                    state.CompleteOne();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancel: leave queued folders unscanned.
        }
    }

    private void Visit(
        FolderNode node,
        ScanProgress progress,
        CancellationToken cancellationToken,
        WorkState state)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            node.MarkIncomplete();
            node.MarkEnumerated();
            return;
        }

        if (node.IsReparsePoint)
        {
            node.MarkEnumerated();
            return;
        }

        progress.SetCurrent(node.FullPath);
        progress.AddDirectory();

        var folderBatch = new List<FolderNode>(DirectoryBatchSize);
        var fileBatch = new List<FileEntry>(FileBatchSize);
        long batchBytes = 0;
        var seen = 0;
        var stoppedEarly = false;

        void Flush()
        {
            if (folderBatch.Count == 0 && fileBatch.Count == 0)
                return;

            var bytes = batchBytes;
            var fileCount = fileBatch.Count;
            var folderCount = folderBatch.Count;
            var toEnqueue = new List<FolderNode>(folderBatch.Count);
            foreach (var folder in folderBatch)
            {
                if (!folder.IsReparsePoint)
                    toEnqueue.Add(folder);
            }

            node.Publish(folderBatch, fileBatch);
            node.AddTotals(bytes, fileCount, folderCount);
            progress.AddFiles(fileCount, bytes);
            folderBatch.Clear();
            fileBatch.Clear();
            batchBytes = 0;

            // Once the user cancels, finish the current folder but don't fan out further.
            if (cancellationToken.IsCancellationRequested)
                return;

            foreach (var folder in toEnqueue)
                state.Enqueue(folder);
        }

        try
        {
            foreach (var entry in _fileSystem.Enumerate(node.FullPath))
            {
                seen++;
                if (seen % FileBatchSize == 0 && cancellationToken.IsCancellationRequested)
                {
                    stoppedEarly = true;
                    break;
                }

                var childPath = PathHelper.Combine(node.FullPath, entry.Name);
                var isReparse = (entry.Attributes & FileAttributes.ReparsePoint) != 0
                    || !string.IsNullOrEmpty(entry.LinkTarget);

                if (entry.IsDirectory)
                {
                    var child = new FolderNode(entry.Name, childPath, node);
                    child.SetTimestamps(
                        FileTime.NormalizeUtc(entry.LastWriteTimeUtc),
                        FileTime.NormalizeUtc(entry.CreationTimeUtc),
                        FileTime.NormalizeUtc(entry.LastAccessTimeUtc));
                    if (isReparse)
                    {
                        child.IsReparsePoint = true;
                        child.LinkTarget = entry.LinkTarget;
                        child.MarkEnumerated();
                    }

                    folderBatch.Add(child);
                    if (folderBatch.Count >= DirectoryBatchSize)
                        Flush();
                    continue;
                }

                // A file symlink reports the target size on Windows and the link-text
                // length on Linux. Either one would double-count or invent bytes.
                // Cloud placeholders are reparse points without a link target; keep those.
                var linkLike = isReparse && !string.IsNullOrEmpty(entry.LinkTarget);
                var size = linkLike ? 0 : Math.Max(0, entry.Length);
                fileBatch.Add(new FileEntry(
                    entry.Name,
                    size,
                    linkLike,
                    linkLike ? entry.LinkTarget : null,
                    FileTime.NormalizeUtc(entry.LastWriteTimeUtc),
                    FileTime.NormalizeUtc(entry.CreationTimeUtc),
                    FileTime.NormalizeUtc(entry.LastAccessTimeUtc)));
                batchBytes += size;
                if (fileBatch.Count >= FileBatchSize)
                    Flush();
            }
        }
        catch (Exception ex) when (IsBenign(ex))
        {
            if (IsDenied(ex))
                node.MarkAccessDenied();
            else if (ex is DirectoryNotFoundException)
                node.Fail("Folder not found.");
            else
                node.Fail(TrimMessage(ex.Message));
        }

        Flush();
        if (stoppedEarly)
            node.MarkIncomplete();
        node.MarkEnumerated();
    }

    private static bool IsBenign(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or ArgumentException
            or NotSupportedException;

    private static bool IsDenied(Exception ex)
    {
        if (ex is UnauthorizedAccessException or System.Security.SecurityException)
            return true;

        if (ex is IOException io)
            return (io.HResult & 0xFFFF) == 5;

        return false;
    }

    private static string TrimMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Could not read this folder.";

        var trimmed = message.Trim();
        return trimmed.Length <= 300 ? trimmed : trimmed[..300];
    }

    private static string SafeName(string path)
    {
        var trimmed = path.Trim();
        return trimmed.Length == 0 ? "(invalid path)" : trimmed;
    }

    /// <summary>
    /// Outstanding work is a refcount: increment before enqueue, decrement when a
    /// folder finishes. The worker that drops it to zero completes the queue.
    /// Children are incremented before the parent decrements, on the same thread.
    /// </summary>
    private sealed class WorkState
    {
        private readonly BlockingCollection<FolderNode> _queue;
        private int _outstanding;

        public WorkState(BlockingCollection<FolderNode> queue) => _queue = queue;

        public void Enqueue(FolderNode node)
        {
            Interlocked.Increment(ref _outstanding);
            try
            {
                _queue.Add(node);
            }
            catch
            {
                if (Interlocked.Decrement(ref _outstanding) == 0)
                    TryComplete();
                throw;
            }
        }

        public void CompleteOne()
        {
            if (Interlocked.Decrement(ref _outstanding) == 0)
                TryComplete();
        }

        private void TryComplete()
        {
            try
            {
                _queue.CompleteAdding();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
