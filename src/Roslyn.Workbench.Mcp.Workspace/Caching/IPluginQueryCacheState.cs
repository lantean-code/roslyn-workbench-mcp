namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal interface IPluginQueryCacheState
{
    QueryCacheScopeIdentity CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName);

    TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull;

    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull;

    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);

    void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
