namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Creates invocation-scoped query caches backed by the shared plugin cache store.
/// </summary>
internal sealed class QueryResultCacheScopeFactory : IQueryResultCacheScopeFactory
{
    private readonly IPluginQueryCacheStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResultCacheScopeFactory"/> class.
    /// </summary>
    /// <param name="store">The shared plugin query cache store.</param>
    public QueryResultCacheScopeFactory(IPluginQueryCacheStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public QueryResultCacheScope CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName)
    {
        var scopeIdentity = _store.CreateScope(
            snapshotIdentity,
            pluginId,
            toolName);

        return new QueryResultCacheScope(_store, scopeIdentity);
    }
}
