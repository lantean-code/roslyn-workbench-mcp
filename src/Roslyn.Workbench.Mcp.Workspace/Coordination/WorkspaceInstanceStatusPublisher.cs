using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed class WorkspaceInstanceStatusPublisher : IWorkspaceInstanceStatusPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly Dictionary<string, WorkspaceInstanceStatusHandle> _handles = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _instanceId = $"{Environment.ProcessId}-{Guid.NewGuid():n}";
    private bool _isDisposed;

    public WorkspaceInstanceStatusPublisher(IFileSystem fileSystem, IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
    }

    public async ValueTask<bool> OpenAsync(
        string workspaceId,
        string workspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isDisposed)
            {
                return false;
            }

            var canonicalWorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            var scan = await PrepareInstanceDirectoryAsync(canonicalWorkspaceRoot, cancellationToken).ConfigureAwait(false);
            if (_handles.ContainsKey(workspaceId))
            {
                return false;
            }

            var handle = CreateHandle(workspaceId, canonicalWorkspaceRoot, loadedPath, state);
            _handles.Add(workspaceId, handle);
            await handle.PublishAsync().ConfigureAwait(false);
            return scan.HasOtherLiveInstance;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<WorkspaceInstanceInfo>> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        try
        {
            var canonicalWorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            var scan = await ScanAsync(canonicalWorkspaceRoot, cancellationToken).ConfigureAwait(false);
            return scan.Instances;
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public async ValueTask UpdateAsync(
        string workspaceId,
        WorkspaceLifecycleState state,
        long? transactionRevision,
        string? commitId,
        string? commitPhase)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isDisposed || !_handles.TryGetValue(workspaceId, out var handle))
            {
                return;
            }

            try
            {
                await handle.UpdateAsync(state, transactionRevision, commitId, commitPhase).ConfigureAwait(false);
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

    public async ValueTask CloseAsync(string workspaceId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
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

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
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

    private async ValueTask<WorkspaceInstanceScanResult> PrepareInstanceDirectoryAsync(
        string canonicalWorkspaceRoot,
        CancellationToken cancellationToken)
    {
        _fileSystem.Directory.CreateDirectory(GetInstanceDirectory(canonicalWorkspaceRoot));
        return await ScanAsync(canonicalWorkspaceRoot, cancellationToken).ConfigureAwait(false);
    }

    private WorkspaceInstanceStatusHandle CreateHandle(
        string workspaceId,
        string canonicalWorkspaceRoot,
        string loadedPath,
        WorkspaceLifecycleState state)
    {
        var directory = GetInstanceDirectory(canonicalWorkspaceRoot);
        var filePath = _fileSystem.Path.Combine(directory, $"{_instanceId}-{workspaceId}.json");
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

    private async ValueTask<WorkspaceInstanceScanResult> ScanAsync(
        string canonicalWorkspaceRoot,
        CancellationToken cancellationToken)
    {
        var directory = GetInstanceDirectory(canonicalWorkspaceRoot);
        if (!_fileSystem.Directory.Exists(directory))
        {
            return WorkspaceInstanceScanResult.Empty;
        }

        var hasOtherLiveInstance = false;
        var instances = new List<WorkspaceInstanceInfo>();
        foreach (var path in _fileSystem.Directory.EnumerateFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryRemoveStaleInstance(path))
            {
                continue;
            }

            var status = await TryReadStatusAsync(path, cancellationToken).ConfigureAwait(false);
            if (status?.InstanceId == _instanceId)
            {
                continue;
            }

            hasOtherLiveInstance = true;
            if (status is not null && IsValidStatus(status, canonicalWorkspaceRoot))
            {
                instances.Add(CreateInstanceInfo(status));
            }
        }

        var orderedInstances = instances
            .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToArray();
        return new WorkspaceInstanceScanResult(hasOtherLiveInstance, orderedInstances);
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
            var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<WorkspaceInstanceStatus>(json, _serializerOptions);
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
            && string.Equals(status.WorkspaceRoot, canonicalWorkspaceRoot, _pathComparison.Comparison);
    }

    private void TryDelete(string path)
    {
        try
        {
            _fileSystem.File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string GetInstanceDirectory(string workspaceRoot)
    {
        return _fileSystem.Path.Combine(workspaceRoot, ".vs", "roslyn-workbench-mcp", "instances");
    }

    private void CloseHandle(WorkspaceInstanceStatusHandle handle)
    {
        handle.Dispose();
        TryDelete(handle.Path);
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
