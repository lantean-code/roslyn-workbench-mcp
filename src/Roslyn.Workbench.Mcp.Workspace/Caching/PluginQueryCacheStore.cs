namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Delegates plugin query-cache operations to the shared lifecycle-aware state.
/// </summary>
internal sealed class PluginQueryCacheStore : IPluginQueryCacheStore
{
    private readonly IPluginQueryCacheState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginQueryCacheStore"/> class.
    /// </summary>
    /// <param name="state">The shared plugin cache state.</param>
    public PluginQueryCacheStore(IPluginQueryCacheState state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public QueryCacheScopeIdentity CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName)
    {
        return _state.CreateScope(snapshotIdentity, pluginId, toolName);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
