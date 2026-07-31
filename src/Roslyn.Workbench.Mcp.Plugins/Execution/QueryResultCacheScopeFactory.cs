namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class QueryResultCacheScopeFactory : IQueryResultCacheScopeFactory
{
    private readonly IPluginQueryCacheStore _store;

    public QueryResultCacheScopeFactory(IPluginQueryCacheStore store)
    {
        _store = store;
    }

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
