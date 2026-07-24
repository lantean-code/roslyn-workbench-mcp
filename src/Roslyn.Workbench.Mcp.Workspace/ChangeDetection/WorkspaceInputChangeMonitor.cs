using System.Threading.Channels;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceInputChangeMonitor : IWorkspaceInputChangeMonitor
{
    private const int _maximumWatcherBufferSize = 64 * 1024;

    private readonly IFileSystemWatcher _watcher;
    private readonly StringComparer _pathComparer;
    private readonly Channel<WorkspaceInputWatcherEvent> _events;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _eventProcessingTask;
    private WorkspaceInputChange? _change;
    private WorkspaceInputPathPolicy _pathPolicy = WorkspaceInputPathPolicy.TrackAll;
    private IReadOnlySet<string>? _trackedDirectories;
    private IReadOnlySet<string>? _trackedFiles;
    private int _disposeState;
    private int _pendingEventCount;

    public WorkspaceInputChange? Change => Volatile.Read(ref _change);

    public WorkspaceInputChangeMonitor(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison,
        string workspaceRoot)
    {
        _pathComparer = pathComparison.GetComparer(workspaceRoot);
        _watcher = fileSystem.FileSystemWatcher.New(workspaceRoot);
        _watcher.IncludeSubdirectories = true;
        _watcher.InternalBufferSize = _maximumWatcherBufferSize;
        _watcher.NotifyFilter = NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            | NotifyFilters.LastWrite
            | NotifyFilters.Size;

        _events = Channel.CreateUnbounded<WorkspaceInputWatcherEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false,
            });

        _watcher.Changed += OnChanged;
        _watcher.Created += OnCreatedOrDeleted;
        _watcher.Deleted += OnCreatedOrDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
        _eventProcessingTask = ProcessEventsAsync(_shutdown.Token);
    }

    public void Track(WorkspaceInputManifest manifest)
    {
        _pathPolicy = manifest.PathPolicy;
        _trackedDirectories = manifest.Directories
            .Select(static directory => directory.Path)
            .ToHashSet(_pathComparer);

        _trackedFiles = manifest.Files
            .Select(static file => file.Path)
            .ToHashSet(_pathComparer);

        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _shutdown.Cancel();
        _events.Writer.TryComplete();

        try
        {
            _eventProcessingTask.GetAwaiter().GetResult();
        }
        finally
        {
            _watcher.Dispose();
            _shutdown.Dispose();
        }
    }

    public void WaitForPendingEvents(CancellationToken cancellationToken)
    {
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _pendingEventCount) > 0 && Change is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        if (_trackedFiles is null)
        {
            return;
        }

        Enqueue(WorkspaceInputWatcherEvent.Changed(args.FullPath));
    }

    private void OnCreatedOrDeleted(object sender, FileSystemEventArgs args)
    {
        if (_trackedFiles is null)
        {
            return;
        }

        WorkspaceInputWatcherEvent watcherEvent;
        if (args.ChangeType == WatcherChangeTypes.Created)
        {
            watcherEvent = WorkspaceInputWatcherEvent.Created(args.FullPath);
        }
        else
        {
            watcherEvent = WorkspaceInputWatcherEvent.Deleted(args.FullPath);
        }

        Enqueue(watcherEvent);
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        if (_trackedFiles is null)
        {
            return;
        }

        var watcherEvent = WorkspaceInputWatcherEvent.Renamed(
            args.FullPath,
            args.OldFullPath);

        Enqueue(watcherEvent);
    }

    private void OnError(object sender, ErrorEventArgs args)
    {
        Enqueue(WorkspaceInputWatcherEvent.WatcherError(args.GetException()));
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var watcherEvent in _events.Reader.ReadAllAsync(cancellationToken))
            {
                if (!TryRecordChange(watcherEvent))
                {
                    Interlocked.Decrement(ref _pendingEventCount);
                    continue;
                }

                Interlocked.Decrement(ref _pendingEventCount);
                _watcher.EnableRaisingEvents = false;
                _events.Writer.TryComplete();
                while (_events.Reader.TryRead(out _))
                {
                    // Discard notifications queued before monitoring was disabled.
                    Interlocked.Decrement(ref _pendingEventCount);
                }

                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Enqueue(WorkspaceInputWatcherEvent watcherEvent)
    {
        Interlocked.Increment(ref _pendingEventCount);
        if (!_events.Writer.TryWrite(watcherEvent))
        {
            Interlocked.Decrement(ref _pendingEventCount);
        }
    }

    private bool TryRecordChange(WorkspaceInputWatcherEvent watcherEvent)
    {
        switch (watcherEvent.Kind)
        {
            case WorkspaceInputWatcherEventKind.Changed:
                if (!ShouldTrackChangedPath(watcherEvent.Path))
                {
                    return false;
                }

                RecordChange(WorkspaceInputChangeKind.Changed, watcherEvent.Path);
                return true;

            case WorkspaceInputWatcherEventKind.Created:
                if (!ShouldTrackCreatedOrDeletedPath(watcherEvent.Path))
                {
                    return false;
                }

                RecordChange(WorkspaceInputChangeKind.Created, watcherEvent.Path);
                return true;

            case WorkspaceInputWatcherEventKind.Deleted:
                if (!ShouldTrackCreatedOrDeletedPath(watcherEvent.Path))
                {
                    return false;
                }

                RecordChange(WorkspaceInputChangeKind.Deleted, watcherEvent.Path);
                return true;

            case WorkspaceInputWatcherEventKind.Renamed:
                if (!ShouldTrackCreatedOrDeletedPath(watcherEvent.PreviousPath)
                    && !ShouldTrackCreatedOrDeletedPath(watcherEvent.Path))
                {
                    return false;
                }

                RecordChange(
                    WorkspaceInputChangeKind.Renamed,
                    watcherEvent.Path,
                    watcherEvent.PreviousPath);

                return true;

            case WorkspaceInputWatcherEventKind.Error:
                var errorCode = watcherEvent.Error is InternalBufferOverflowException
                    ? WorkspaceInputChangeErrorCode.WatcherBufferOverflow
                    : WorkspaceInputChangeErrorCode.WatcherFailure;

                RecordChange(
                    WorkspaceInputChangeKind.WatcherError,
                    errorCode: errorCode);

                return true;

            default:
                return false;
        }
    }

    private bool ShouldTrackChangedPath(string? path)
    {
        var trackedFiles = _trackedFiles;
        return path is not null
            && trackedFiles is not null
            && trackedFiles.Contains(path);
    }

    private bool ShouldTrackCreatedOrDeletedPath(string? path)
    {
        var trackedFiles = _trackedFiles;
        var trackedDirectories = _trackedDirectories;
        if (path is null || trackedFiles is null || trackedDirectories is null)
        {
            return false;
        }

        if (trackedFiles.Contains(path) || trackedDirectories.Contains(path))
        {
            return true;
        }

        if (!_pathPolicy.ShouldTrack(path))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(path);
        return parent is not null && trackedDirectories.Contains(parent);
    }

    private void RecordChange(
        WorkspaceInputChangeKind kind,
        string? path = null,
        string? previousPath = null,
        WorkspaceInputChangeErrorCode? errorCode = null)
    {
        var change = new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            ErrorCode = errorCode,
            Kind = kind,
            Path = path,
            PreviousPath = previousPath,
        };

        Interlocked.CompareExchange(ref _change, change, null);
    }
}
