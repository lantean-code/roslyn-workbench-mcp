namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Provides plugin execution adapters with scoped access to the shared plugin query cache.
/// </summary>
internal interface IPluginQueryCacheStore
{
    /// <inheritdoc cref="IPluginQueryCacheState.CreateScope(WorkspaceSnapshotIdentity, string, string)"/>
    QueryCacheScopeIdentity CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName);

    /// <inheritdoc cref="IPluginQueryCacheState.GetOrCreate{TKey, TValue}(QueryCacheScopeIdentity, TKey, Func{CancellationToken, TValue}, CancellationToken)"/>
    TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull;

    /// <inheritdoc cref="IPluginQueryCacheState.GetOrCreateAsync{TKey, TValue}(QueryCacheScopeIdentity, TKey, Func{CancellationToken, ValueTask{TValue}}, CancellationToken)"/>
    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull;
}
