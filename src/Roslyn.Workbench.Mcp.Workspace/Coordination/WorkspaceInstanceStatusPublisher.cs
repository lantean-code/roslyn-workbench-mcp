using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed class WorkspaceInstanceStatusPublisher : IWorkspaceInstanceStatusPublisher, IDisposable
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly Dictionary<string, InstanceLease> _leases = new(StringComparer.Ordinal);
    private readonly string _instanceId = $"{Environment.ProcessId}-{Guid.NewGuid():n}";

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
        try
        {
            var canonicalWorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
            var directory = GetInstanceDirectory(canonicalWorkspaceRoot);
            _fileSystem.Directory.CreateDirectory(directory);
            var scan = await ScanAsync(canonicalWorkspaceRoot, cancellationToken).ConfigureAwait(false);

            var filePath = _fileSystem.Path.Combine(directory, $"{_instanceId}-{workspaceId}.json");
            var stream = _fileSystem.FileStream.New(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            var lease = new InstanceLease(filePath, stream, new WorkspaceInstanceStatus
            {
                InstanceId = _instanceId,
                LoadedPath = _fileSystem.Path.GetFullPath(loadedPath),
                WorkspaceRoot = canonicalWorkspaceRoot,
                WorkspaceState = state,
            });
            _leases.Add(workspaceId, lease);
            await WriteAsync(lease).ConfigureAwait(false);
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
    }

    public async ValueTask<IReadOnlyList<WorkspaceInstanceInfo>> GetOtherLiveInstancesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        try
        {
            return (await ScanAsync(_fileSystem.Path.GetFullPath(workspaceRoot), cancellationToken).ConfigureAwait(false)).Instances;
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

    public async ValueTask UpdateAsync(string workspaceId, WorkspaceLifecycleState state, long? transactionRevision, string? commitId, string? commitPhase)
    {
        if (!_leases.TryGetValue(workspaceId, out var lease))
        {
            return;
        }

        lease.Status = lease.Status with
        {
            WorkspaceState = state,
            TransactionRevision = transactionRevision,
            CommitId = commitId,
            CommitPhase = commitPhase,
        };
        try
        {
            await WriteAsync(lease).ConfigureAwait(false);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Close(string workspaceId)
    {
        if (!_leases.Remove(workspaceId, out var lease))
        {
            return;
        }

        lease.Stream.Dispose();
        try
        {
            _fileSystem.File.Delete(lease.Path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        foreach (var workspaceId in _leases.Keys.ToArray())
        {
            Close(workspaceId);
        }
    }

    private static async ValueTask WriteAsync(InstanceLease lease)
    {
        lease.Stream.Position = 0;
        lease.Stream.SetLength(0);
        await JsonSerializer.SerializeAsync(lease.Stream, lease.Status, _options, CancellationToken.None).ConfigureAwait(false);
        await lease.Stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        lease.Stream.Flush();
    }

    private async ValueTask<(bool HasOtherLiveInstance, IReadOnlyList<WorkspaceInstanceInfo> Instances)> ScanAsync(
        string canonicalWorkspaceRoot,
        CancellationToken cancellationToken)
    {
        var directory = GetInstanceDirectory(canonicalWorkspaceRoot);
        if (!_fileSystem.Directory.Exists(directory))
        {
            return (false, []);
        }

        var hasOtherLiveInstance = false;
        var instances = new List<WorkspaceInstanceInfo>();
        foreach (var path in _fileSystem.Directory.EnumerateFiles(directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (_fileSystem.FileStream.New(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                _fileSystem.File.Delete(path);
                continue;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            WorkspaceInstanceStatus? status = null;
            try
            {
                var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                status = JsonSerializer.Deserialize<WorkspaceInstanceStatus>(json, _options);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }

            if (status?.InstanceId == _instanceId)
            {
                continue;
            }

            hasOtherLiveInstance = true;
            if (status is null || status.Version != 2 || !PathsEqual(status.WorkspaceRoot, canonicalWorkspaceRoot))
            {
                continue;
            }

            instances.Add(new WorkspaceInstanceInfo
            {
                InstanceId = status.InstanceId,
                LoadedPath = status.LoadedPath,
                WorkspaceRoot = status.WorkspaceRoot,
                WorkspaceState = status.WorkspaceState,
                TransactionRevision = status.TransactionRevision,
                CommitId = status.CommitId,
                CommitPhase = status.CommitPhase,
            });
        }

        return (hasOtherLiveInstance, instances.OrderBy(instance => instance.InstanceId, StringComparer.Ordinal).ToArray());
    }

    private string GetInstanceDirectory(string workspaceRoot)
    {
        return _fileSystem.Path.Combine(workspaceRoot, ".vs", "roslyn-workbench-mcp", "instances");
    }

    private bool PathsEqual(string left, string right)
    {
        return string.Equals(left, right, _pathComparison.Comparison);
    }

    private sealed class InstanceLease
    {
        public string Path { get; }

        public Stream Stream { get; }

        public WorkspaceInstanceStatus Status { get; set; }

        public InstanceLease(string path, Stream stream, WorkspaceInstanceStatus status)
        {
            Path = path;
            Stream = stream;
            Status = status;
        }
    }
}
