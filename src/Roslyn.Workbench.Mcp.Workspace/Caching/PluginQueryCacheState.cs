using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class PluginQueryCacheState : IPluginQueryCacheState, IDisposable
{
    private readonly QueryCacheStateCore _core;
    private readonly HashSet<WorkspaceSnapshotIdentity> _partitions;
    private readonly Lock _syncRoot;

    public PluginQueryCacheState(
        IOptions<PluginQueryCacheOptions> options,
        IHostApplicationLifetime applicationLifetime)
    {
        _core = new QueryCacheStateCore(
            options.Value.EntryLimit,
            options.Value.SlidingExpiration,
            applicationLifetime,
            WorkbenchPerformanceEventSource.PluginQueryCacheFamily);

        _partitions = [];
        _syncRoot = new Lock();
    }

    public QueryCacheScopeIdentity CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName)
    {
        lock (_syncRoot)
        {
            _partitions.Add(snapshotIdentity);
        }

        var scope = new PluginQueryScope(pluginId, toolName);
        return _core.CreateScope(snapshotIdentity, scope);
    }

    public TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull
    {
        return _core.GetOrCreate(
            scopeIdentity,
            key,
            valueFactory,
            static _ => 1,
            static value => value is not IDisposable and not IAsyncDisposable,
            cancellationToken);
    }

    public ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull
    {
        return _core.GetOrCreateAsync(
            scopeIdentity,
            key,
            valueFactory,
            static _ => 1,
            static value => value is not IDisposable and not IAsyncDisposable,
            cancellationToken);
    }

    public void InvalidateWorkspace(string workspaceId, long workspaceEpoch)
    {
        InvalidateMatching(snapshot =>
            string.Equals(snapshot.WorkspaceId, workspaceId, StringComparison.Ordinal)
            && snapshot.WorkspaceEpoch == workspaceEpoch);
    }

    public void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        InvalidateMatching(snapshot =>
            string.Equals(snapshot.WorkspaceId, workspaceId, StringComparison.Ordinal)
            && snapshot.WorkspaceEpoch == workspaceEpoch
            && snapshot.TransactionId == transactionId);
    }

    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            Invalidate(snapshot);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _partitions.Clear();
        }

        _core.Dispose();
    }

    private void InvalidateMatching(Func<WorkspaceSnapshotIdentity, bool> predicate)
    {
        WorkspaceSnapshotIdentity[] snapshots;
        lock (_syncRoot)
        {
            snapshots = [.. _partitions.Where(predicate)];
        }

        foreach (var snapshot in snapshots)
        {
            Invalidate(snapshot);
        }
    }

    private void Invalidate(WorkspaceSnapshotIdentity snapshot)
    {
        lock (_syncRoot)
        {
            _partitions.Remove(snapshot);
        }

        _core.InvalidatePartition(snapshot);
    }

    private sealed record PluginQueryScope
    {
        public string PluginId { get; }

        public string ToolName { get; }

        public PluginQueryScope(string pluginId, string toolName)
        {
            PluginId = pluginId;
            ToolName = toolName;
        }
    }
}
