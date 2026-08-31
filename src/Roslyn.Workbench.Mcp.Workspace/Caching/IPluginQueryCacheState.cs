namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Owns plugin query-cache generations and invalidates them as Workspace snapshots and transactions expire.
/// </summary>
internal interface IPluginQueryCacheState
{
    /// <summary>
    /// Creates a cache scope isolated by Workspace snapshot, plugin and tool.
    /// </summary>
    /// <param name="snapshotIdentity">The immutable Workspace snapshot represented by the scope.</param>
    /// <param name="pluginId">The stable identifier of the plugin using the cache.</param>
    /// <param name="toolName">The registered name of the tool using the cache.</param>
    /// <returns>An identity that can address entries within the isolated scope.</returns>
    QueryCacheScopeIdentity CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName);

    /// <summary>
    /// Returns a cached value or runs a shared synchronous factory for a missing entry.
    /// </summary>
    /// <typeparam name="TKey">The cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="valueFactory">The factory used to create the required value.</param>
    /// <param name="cancellationToken">Cancels this caller's wait for the value.</param>
    /// <returns>The cached or produced value, or <see langword="null"/> when the factory produces no value.</returns>
    TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull;

    /// <summary>
    /// Returns a cached value or runs a shared asynchronous factory for a missing entry.
    /// </summary>
    /// <typeparam name="TKey">The cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="valueFactory">The factory used to create the required value.</param>
    /// <param name="cancellationToken">Cancels this caller's wait for the value.</param>
    /// <returns>The cached or produced value, or <see langword="null"/> when the factory produces no value.</returns>
    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull;

    /// <summary>
    /// Invalidates every plugin cache generation for one loaded Workspace epoch.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier.</param>
    /// <param name="workspaceEpoch">The load epoch to invalidate.</param>
    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);

    /// <summary>
    /// Invalidates plugin cache generations associated with one transaction.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier.</param>
    /// <param name="workspaceEpoch">The load epoch containing the transaction.</param>
    /// <param name="transactionId">The transaction identifier.</param>
    void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    /// <summary>
    /// Invalidates the specified snapshot generations.
    /// </summary>
    /// <param name="snapshots">The snapshot identities to invalidate.</param>
    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
