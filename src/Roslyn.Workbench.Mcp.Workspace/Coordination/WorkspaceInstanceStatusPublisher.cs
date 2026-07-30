using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed class WorkspaceInstanceStatusPublisher : IWorkspaceInstanceStatusPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly Dictionary<string, WorkspaceInstanceStatusHandle> _handles = new(StringComparer.Ordinal);
    private readonly Channel<WorkspaceInstanceStatusUpdate> _updates;
    private readonly Task _updateWorker;
    private readonly Lock _updateSync = new();

    [SuppressMessage(
        "Usage",
        "CA2213:Disposable fields should be disposed",
        Justification = "The semaphore remains usable after logical disposal so queued and repeated lifecycle calls can observe disposed state without ObjectDisposedException; AvailableWaitHandle is never accessed.")]
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly string _instanceId = $"{Environment.ProcessId}-{Guid.NewGuid():n}";
    private bool _isDisposed;

    public WorkspaceInstanceStatusPublisher(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison,
        IPhysicalPathContainment pathContainment)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
        _pathContainment = pathContainment;
        _updates = Channel.CreateUnbounded<WorkspaceInstanceStatusUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _updateWorker = ProcessUpdatesAsync();
    }

    public async ValueTask<WorkspaceInstanceStatusResult> OpenAsync(
        string workspaceId,
        string workspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_isDisposed)
            {
                return WorkspaceInstanceStatusResult.Unavailable;
            }

            var canonicalWorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            if (!TryGetInstanceDirectory(canonicalWorkspaceRoot, out var instanceDirectory))
            {
                return WorkspaceInstanceStatusResult.Unavailable;
            }

            var scan = await PrepareInstanceDirectoryAsync(
                canonicalWorkspaceRoot,
                instanceDirectory,
                cancellationToken);

            if (!scan.IsAvailable)
            {
                return scan;
            }

            if (_handles.ContainsKey(workspaceId))
            {
                return WorkspaceInstanceStatusResult.Empty;
            }

            var handle = CreateHandle(
                workspaceId,
                canonicalWorkspaceRoot,
                loadedPath,
                state,
                instanceDirectory);

            _handles.Add(workspaceId, handle);
            await handle.PublishAsync();
            return scan;
        }
        catch (IOException)
        {
            return WorkspaceInstanceStatusResult.Unavailable;
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceInstanceStatusResult.Unavailable;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<WorkspaceInstanceStatusResult> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        try
        {
            var canonicalWorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            if (!TryGetInstanceDirectory(canonicalWorkspaceRoot, out var instanceDirectory))
            {
                return WorkspaceInstanceStatusResult.Unavailable;
            }

            return await ScanAsync(canonicalWorkspaceRoot, instanceDirectory, cancellationToken);
        }
        catch (IOException)
        {
            return WorkspaceInstanceStatusResult.Unavailable;
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceInstanceStatusResult.Unavailable;
        }
    }

    public ValueTask UpdateAsync(
        string workspaceId,
        WorkspaceLifecycleState state,
        long? transactionRevision,
        string? commitId,
        string? commitPhase)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_updateSync)
        {
            if (!_updates.Writer.TryWrite(CreateUpdate(
                workspaceId,
                state,
                transactionRevision,
                commitId,
                commitPhase,
                completion)))
            {
                completion.SetResult();
            }
        }

        return new ValueTask(completion.Task);
    }

    public void QueueUpdate(
        string workspaceId,
        WorkspaceLifecycleState state,
        long? transactionRevision,
        string? commitId,
        string? commitPhase)
    {
        lock (_updateSync)
        {
            _updates.Writer.TryWrite(CreateUpdate(
                workspaceId,
                state,
                transactionRevision,
                commitId,
                commitPhase,
                completion: null));
        }
    }

    public async ValueTask CloseAsync(string workspaceId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_handles.Remove(workspaceId, out var handle))
            {
                return;
            }

            CloseHandle(handle);
        }
        finally
        {
            _gate.Release();
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Disposal must attempt every workspace status handle, retain the first failure, and rethrow it after all handles have been closed.")]
    public async ValueTask DisposeAsync()
    {
        lock (_updateSync)
        {
            _updates.Writer.TryComplete();
        }

        await _updateWorker;

        await _gate.WaitAsync();
        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            var handles = _handles.Values.ToArray();
            _handles.Clear();

            ExceptionDispatchInfo? disposalFailure = null;
            foreach (var handle in handles)
            {
                try
                {
                    CloseHandle(handle);
                }
                catch (Exception exception)
                {
                    disposalFailure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }

            disposalFailure?.Throw();
        }
        finally
        {
            _gate.Release();
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The owned update worker must complete awaited updates with their failure and continue draining later advisory writes before shutdown.")]
    private async Task ProcessUpdatesAsync()
    {
        await foreach (var update in _updates.Reader.ReadAllAsync())
        {
            try
            {
                await ApplyUpdateAsync(update);
                update.Completion?.SetResult();
            }
            catch (Exception exception)
            {
                update.Completion?.SetException(exception);
            }
        }
    }

    private async ValueTask ApplyUpdateAsync(WorkspaceInstanceStatusUpdate update)
    {
        await _gate.WaitAsync();
        try
        {
            if (_isDisposed || !_handles.TryGetValue(update.WorkspaceId, out var handle))
            {
                return;
            }

            try
            {
                await handle.UpdateAsync(
                    update.State,
                    update.TransactionRevision,
                    update.CommitId,
                    update.CommitPhase);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static WorkspaceInstanceStatusUpdate CreateUpdate(
        string workspaceId,
        WorkspaceLifecycleState state,
        long? transactionRevision,
        string? commitId,
        string? commitPhase,
        TaskCompletionSource? completion)
    {
        return new WorkspaceInstanceStatusUpdate
        {
            WorkspaceId = workspaceId,
            State = state,
            TransactionRevision = transactionRevision,
            CommitId = commitId,
            CommitPhase = commitPhase,
            Completion = completion,
        };
    }

    private async ValueTask<WorkspaceInstanceStatusResult> PrepareInstanceDirectoryAsync(
        string canonicalWorkspaceRoot,
        string instanceDirectory,
        CancellationToken cancellationToken)
    {
        _fileSystem.Directory.CreateDirectory(instanceDirectory);
        if (!_pathContainment.TryGetStrictlyContainedPath(
            canonicalWorkspaceRoot,
            instanceDirectory,
            out instanceDirectory))
        {
            return WorkspaceInstanceStatusResult.Unavailable;
        }

        return await ScanAsync(canonicalWorkspaceRoot, instanceDirectory, cancellationToken);
    }

    private WorkspaceInstanceStatusHandle CreateHandle(
        string workspaceId,
        string canonicalWorkspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        string instanceDirectory)
    {
        var filePath = _fileSystem.Path.Combine(instanceDirectory, $"{_instanceId}-{workspaceId}.json");
        var stream = _fileSystem.FileStream.New(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        var status = new WorkspaceInstanceStatus
        {
            InstanceId = _instanceId,
            LoadedPath = _fileSystem.Path.GetFullPath(loadedPath),
            WorkspaceRoot = canonicalWorkspaceRoot,
            WorkspaceState = state,
        };

        return new WorkspaceInstanceStatusHandle(filePath, stream, status, _serializerOptions);
    }

    private async ValueTask<WorkspaceInstanceStatusResult> ScanAsync(
        string canonicalWorkspaceRoot,
        string instanceDirectory,
        CancellationToken cancellationToken)
    {
        if (!_fileSystem.Directory.Exists(instanceDirectory))
        {
            return WorkspaceInstanceStatusResult.Empty;
        }

        var hasOtherLiveInstance = false;
        var hasUnreadableLiveInstance = false;
        var instances = new List<WorkspaceInstanceInfo>();
        foreach (var path in _fileSystem.Directory.EnumerateFiles(instanceDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_pathContainment.TryGetStrictlyContainedPath(
                canonicalWorkspaceRoot,
                path,
                out var containedPath))
            {
                hasOtherLiveInstance = true;
                hasUnreadableLiveInstance = true;
                continue;
            }

            if (TryRemoveStaleInstance(containedPath))
            {
                continue;
            }

            var status = await TryReadStatusAsync(containedPath, cancellationToken);
            if (status is not null && IsValidStatus(status, canonicalWorkspaceRoot))
            {
                if (status.InstanceId == _instanceId)
                {
                    continue;
                }

                hasOtherLiveInstance = true;
                instances.Add(CreateInstanceInfo(status));
                continue;
            }

            hasOtherLiveInstance = true;
            hasUnreadableLiveInstance = true;
        }

        var orderedInstances = instances
            .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToArray();

        return new WorkspaceInstanceStatusResult(
            isAvailable: true,
            hasOtherLiveInstance,
            hasUnreadableLiveInstance,
            orderedInstances);
    }

    private bool TryRemoveStaleInstance(string path)
    {
        try
        {
            using (_fileSystem.FileStream.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }

            _fileSystem.File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async ValueTask<WorkspaceInstanceStatus?> TryReadStatusAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = _fileSystem.FileStream.New(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            return await JsonSerializer.DeserializeAsync<WorkspaceInstanceStatus>(
                stream,
                _serializerOptions,
                cancellationToken);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool IsValidStatus(WorkspaceInstanceStatus status, string canonicalWorkspaceRoot)
    {
        return status.Version == 2
            && string.Equals(
                status.WorkspaceRoot,
                canonicalWorkspaceRoot,
                _pathComparison.GetComparison(canonicalWorkspaceRoot));
    }

    private void TryDelete(string workspaceRoot, string path)
    {
        try
        {
            if (_pathContainment.TryGetStrictlyContainedPath(
                workspaceRoot,
                path,
                out var containedPath))
            {
                _fileSystem.File.Delete(containedPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private bool TryGetInstanceDirectory(
        string workspaceRoot,
        [NotNullWhen(true)] out string? instanceDirectory)
    {
        var candidateDirectory = _fileSystem.Path.Combine(
            workspaceRoot,
            ".vs",
            "roslyn-workbench-mcp",
            "instances");

        return _pathContainment.TryGetStrictlyContainedPath(
            workspaceRoot,
            candidateDirectory,
            out instanceDirectory);
    }

    private void CloseHandle(WorkspaceInstanceStatusHandle handle)
    {
        handle.Dispose();
        TryDelete(handle.WorkspaceRoot, handle.Path);
    }

    private static WorkspaceInstanceInfo CreateInstanceInfo(WorkspaceInstanceStatus status)
    {
        return new WorkspaceInstanceInfo
        {
            InstanceId = status.InstanceId,
            LoadedPath = status.LoadedPath,
            WorkspaceRoot = status.WorkspaceRoot,
            WorkspaceState = status.WorkspaceState,
            TransactionRevision = status.TransactionRevision,
            CommitId = status.CommitId,
            CommitPhase = status.CommitPhase,
        };
    }
}
