using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceExternalInputChangeMonitor : IWorkspaceExternalInputChangeMonitor
{
    private const int _maximumWatcherBufferSize = 64 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IReadOnlyList<WorkspaceExternalInputMembership> _memberships;
    private readonly Channel<WorkspaceInputWatcherEvent> _events;
    private readonly HashSet<FileSystemPathKey> _pendingMembershipChecks = [];
    private readonly Dictionary<FileSystemPathKey, IFileSystemWatcher> _watchers = [];
    private readonly object _membershipLock = new();
    private readonly Task _eventProcessingTask;
    private WorkspaceInputChange? _change;
    private int _disposeState;
    private int _initialMembershipCheckState;
    private int _pendingEventCount;
    private int _startState;

    public WorkspaceInputChange? Change => Volatile.Read(ref _change);

    public WorkspaceExternalInputChangeMonitor(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison,
        IReadOnlyList<WorkspaceExternalInputMembership> memberships)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
        _memberships = memberships;
        _events = Channel.CreateUnbounded<WorkspaceInputWatcherEvent>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false,
            });

        _eventProcessingTask = ProcessEventsAsync();
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _startState, 1) != 0)
        {
            return;
        }

        lock (_membershipLock)
        {
            foreach (var membership in _memberships)
            {
                if (_fileSystem.Directory.Exists(membership.SearchRoot))
                {
                    TryStartWatcherUnderLock(membership.SearchRoot);
                }
            }

            WorkbenchPerformanceEventSource.Log.WorkspaceInputMonitorConfigured(
                _memberships.Count,
                _memberships.Sum(static membership => membership.Globs.Count),
                _watchers.Count);
        }
    }

    public void WaitForPendingEvents(CancellationToken cancellationToken)
    {
        WaitForEventProcessing(cancellationToken);
        CheckMemberships(cancellationToken);
        WaitForEventProcessing(cancellationToken);
        if (HasPendingMembershipChecks())
        {
            CheckMemberships(cancellationToken);
            WaitForEventProcessing(cancellationToken);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        DisableWatchers();
        _events.Writer.TryComplete();

        try
        {
            _eventProcessingTask.GetAwaiter().GetResult();
        }
        finally
        {
            lock (_membershipLock)
            {
                foreach (var watcher in _watchers.Values)
                {
                    watcher.Dispose();
                }

                _watchers.Clear();
            }
        }
    }

    private void WaitForEventProcessing(CancellationToken cancellationToken)
    {
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _pendingEventCount) > 0 && Change is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinWait.SpinOnce();
        }
    }

    private bool HasPendingMembershipChecks()
    {
        lock (_membershipLock)
        {
            return _pendingMembershipChecks.Count > 0;
        }
    }

    private void CheckMemberships(CancellationToken cancellationToken)
    {
        if (_memberships.Count == 0 || Change is not null)
        {
            return;
        }

        using var phase = WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace",
            WorkbenchPerformanceEventSource.ExternalMembershipCheckPhase);

        WorkspaceInputChange? detectedChange = null;
        lock (_membershipLock)
        {
            var requiresInitialCheck = Volatile.Read(ref _initialMembershipCheckState) == 0;
            foreach (var membership in _memberships)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rootKey = _pathComparison.CreateKey(membership.SearchRoot);
                var requiresEventCheck = _pendingMembershipChecks.Contains(rootKey);
                var isWatched = _watchers.ContainsKey(rootKey);
                var rootExists = _fileSystem.Directory.Exists(membership.SearchRoot);
                if (!rootExists && isWatched)
                {
                    StopWatcherUnderLock(rootKey);
                    isWatched = false;
                }

                if (!requiresInitialCheck && !requiresEventCheck && isWatched)
                {
                    continue;
                }

                if (rootExists && !isWatched)
                {
                    TryStartWatcherUnderLock(membership.SearchRoot);
                }

                try
                {
                    if (TryFindMembershipChange(membership, rootExists, cancellationToken, out var change))
                    {
                        detectedChange = change;
                        break;
                    }

                    _pendingMembershipChecks.Remove(rootKey);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    detectedChange = CreateMembershipFailure();
                    break;
                }
            }

            if (detectedChange is null)
            {
                Volatile.Write(ref _initialMembershipCheckState, 1);
            }
        }

        if (detectedChange is not null)
        {
            RecordChange(detectedChange);
        }
    }

    private bool TryFindMembershipChange(
        WorkspaceExternalInputMembership membership,
        bool rootExists,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out WorkspaceInputChange? change)
    {
        var currentPaths = new HashSet<FileSystemPathKey>();
        if (rootExists)
        {
            foreach (var path in _fileSystem.Directory.EnumerateFiles(
                membership.SearchRoot,
                "*",
                SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (membership.Matches(path))
                {
                    currentPaths.Add(_pathComparison.CreateKey(path));
                }
            }
        }

        if (TryFindFirstDifference(currentPaths, membership.LoadedPaths, out var createdPath))
        {
            change = CreateMembershipChange(WorkspaceInputChangeKind.Created, createdPath.Path);
            return true;
        }

        if (TryFindFirstDifference(membership.LoadedPaths, currentPaths, out var deletedPath))
        {
            change = CreateMembershipChange(WorkspaceInputChangeKind.Deleted, deletedPath.Path);
            return true;
        }

        change = null;
        return false;
    }

    private static bool TryFindFirstDifference(
        IEnumerable<FileSystemPathKey> candidates,
        IReadOnlySet<FileSystemPathKey> comparison,
        out FileSystemPathKey difference)
    {
        difference = default;
        var foundDifference = false;
        foreach (var candidate in candidates)
        {
            if (comparison.Contains(candidate))
            {
                continue;
            }

            if (!foundDifference || string.CompareOrdinal(candidate.Path, difference.Path) < 0)
            {
                difference = candidate;
                foundDifference = true;
            }
        }

        return foundDifference;
    }

    private bool TryStartWatcherUnderLock(string searchRoot)
    {
        var rootKey = _pathComparison.CreateKey(searchRoot);
        if (_watchers.ContainsKey(rootKey))
        {
            return true;
        }

        IFileSystemWatcher? watcher = null;
        try
        {
            watcher = CreateWatcher(searchRoot);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(rootKey, watcher);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            watcher?.Dispose();
            return false;
        }
    }

    private IFileSystemWatcher CreateWatcher(string searchRoot)
    {
        var watcher = _fileSystem.FileSystemWatcher.New(searchRoot);
        try
        {
            watcher.IncludeSubdirectories = true;
            watcher.InternalBufferSize = _maximumWatcherBufferSize;
            watcher.NotifyFilter = NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnCreatedOrDeleted;
            watcher.Deleted += OnCreatedOrDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            return watcher;
        }
        catch
        {
            watcher.Dispose();
            throw;
        }
    }

    private void StopWatcherUnderLock(FileSystemPathKey rootKey)
    {
        var watcher = _watchers[rootKey];
        _watchers.Remove(rootKey);
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        Enqueue(WorkspaceInputWatcherEvent.Changed(args.FullPath));
    }

    private void OnCreatedOrDeleted(object sender, FileSystemEventArgs args)
    {
        var watcherEvent = args.ChangeType == WatcherChangeTypes.Created
            ? WorkspaceInputWatcherEvent.Created(args.FullPath)
            : WorkspaceInputWatcherEvent.Deleted(args.FullPath);

        Enqueue(watcherEvent);
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        Enqueue(WorkspaceInputWatcherEvent.Renamed(args.FullPath, args.OldFullPath));
    }

    private void OnError(object sender, ErrorEventArgs args)
    {
        Enqueue(WorkspaceInputWatcherEvent.WatcherError(args.GetException()));
    }

    private void Enqueue(WorkspaceInputWatcherEvent watcherEvent)
    {
        Interlocked.Increment(ref _pendingEventCount);
        if (!_events.Writer.TryWrite(watcherEvent))
        {
            Interlocked.Decrement(ref _pendingEventCount);
        }
    }

    private async Task ProcessEventsAsync()
    {
        await foreach (var watcherEvent in _events.Reader.ReadAllAsync())
        {
            var change = TryCreateChange(watcherEvent);
            if (change is null)
            {
                Interlocked.Decrement(ref _pendingEventCount);
                continue;
            }

            RecordChange(change);
            Interlocked.Decrement(ref _pendingEventCount);
            while (_events.Reader.TryRead(out _))
            {
                Interlocked.Decrement(ref _pendingEventCount);
            }

            return;
        }
    }

    private WorkspaceInputChange? TryCreateChange(WorkspaceInputWatcherEvent watcherEvent)
    {
        if (!watcherEvent.HasPath)
        {
            var errorCode = watcherEvent.Error is InternalBufferOverflowException
                ? WorkspaceInputChangeErrorCode.WatcherBufferOverflow
                : WorkspaceInputChangeErrorCode.WatcherFailure;

            return new WorkspaceInputChange
            {
                DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
                ErrorCode = errorCode,
                Kind = WorkspaceInputChangeKind.WatcherError,
            };
        }

        var pathMatches = Matches(watcherEvent.Path);
        var previousPathMatches = watcherEvent.HasPreviousPath
            && Matches(watcherEvent.PreviousPath);
        if (!pathMatches && !previousPathMatches)
        {
            QueueDirectoryMembershipChecks(watcherEvent, watcherEvent.Path);
            return null;
        }

        return new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.FileSystemWatcher,
            Kind = watcherEvent.Kind,
            Path = watcherEvent.Path,
            PreviousPath = watcherEvent.PreviousPath,
        };
    }

    private void QueueDirectoryMembershipChecks(
        WorkspaceInputWatcherEvent watcherEvent,
        string path)
    {
        lock (_membershipLock)
        {
            foreach (var membership in _memberships)
            {
                var currentDirectoryRequiresCheck = RequiresCurrentDirectoryMembershipCheck(
                    watcherEvent.Kind,
                    path,
                    membership);

                var deletedDirectoryRequiresCheck = RequiresDeletedDirectoryMembershipCheck(
                    watcherEvent.Kind,
                    path,
                    membership);

                var renamedDirectoryRequiresCheck = RequiresRenamedDirectoryMembershipCheck(
                    watcherEvent,
                    membership);

                if (currentDirectoryRequiresCheck || deletedDirectoryRequiresCheck || renamedDirectoryRequiresCheck)
                {
                    _pendingMembershipChecks.Add(_pathComparison.CreateKey(membership.SearchRoot));
                }
            }
        }
    }

    private bool RequiresCurrentDirectoryMembershipCheck(
        WorkspaceInputChangeKind changeKind,
        string path,
        WorkspaceExternalInputMembership membership)
    {
        if (changeKind is not (WorkspaceInputChangeKind.Created or WorkspaceInputChangeKind.Renamed))
        {
            return false;
        }

        return _fileSystem.Directory.Exists(path)
            && membership.Contains(path);
    }

    private static bool RequiresDeletedDirectoryMembershipCheck(
        WorkspaceInputChangeKind changeKind,
        string path,
        WorkspaceExternalInputMembership membership)
    {
        return changeKind == WorkspaceInputChangeKind.Deleted
            && membership.ContainsLoadedPathWithin(path);
    }

    private static bool RequiresRenamedDirectoryMembershipCheck(
        WorkspaceInputWatcherEvent watcherEvent,
        WorkspaceExternalInputMembership membership)
    {
        return watcherEvent.Kind == WorkspaceInputChangeKind.Renamed
            && watcherEvent.HasPreviousPath
            && membership.ContainsLoadedPathWithin(watcherEvent.PreviousPath);
    }

    private bool Matches(string path)
    {
        foreach (var membership in _memberships)
        {
            if (membership.Matches(path))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordChange(WorkspaceInputChange change)
    {
        if (Interlocked.CompareExchange(ref _change, change, null) is null)
        {
            DisableWatchers();
            _events.Writer.TryComplete();
        }
    }

    private void DisableWatchers()
    {
        lock (_membershipLock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
            }
        }
    }

    private static WorkspaceInputChange CreateMembershipChange(WorkspaceInputChangeKind kind, string path)
    {
        return new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            Kind = kind,
            Path = path,
        };
    }

    private static WorkspaceInputChange CreateMembershipFailure()
    {
        return new WorkspaceInputChange
        {
            DetectionSource = WorkspaceInputChangeDetectionSource.MetadataPolling,
            ErrorCode = WorkspaceInputChangeErrorCode.MembershipEnumerationFailure,
            Kind = WorkspaceInputChangeKind.MembershipError,
        };
    }
}
