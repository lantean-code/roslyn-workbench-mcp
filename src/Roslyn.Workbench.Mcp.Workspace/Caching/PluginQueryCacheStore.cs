namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class PluginQueryCacheStore : IPluginQueryCacheStore
{
    private readonly IPluginQueryCacheState _state;

    public PluginQueryCacheStore(IPluginQueryCacheState state)
    {
        _state = state;
    }

    public QueryCacheScopeIdentity CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName)
    {
        return _state.CreateScope(snapshotIdentity, pluginId, toolName);
    }

    public TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class
        where TValue : notnull
    {
        return _state.GetOrCreate(
            scopeIdentity,
            key,
            valueFactory,
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
        return _state.GetOrCreateAsync(
            scopeIdentity,
            key,
            valueFactory,
            cancellationToken);
    }
}
