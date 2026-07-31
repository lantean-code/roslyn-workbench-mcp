namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class QueryResultCacheScope : IQueryResultCache, IDisposable
{
    private readonly QueryCacheScopeIdentity _scopeIdentity;
    private readonly IPluginQueryCacheStore _store;
    private int _isActive;

    public QueryResultCacheScope(
        IPluginQueryCacheStore store,
        QueryCacheScopeIdentity scopeIdentity)
    {
        _store = store;
        _scopeIdentity = scopeIdentity;
        _isActive = 1;
    }

    public TValue? GetOrCreate<TKey, TValue>(
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class, IQueryResultCacheKey
        where TValue : notnull
    {
        ThrowIfInactive();
        return _store.GetOrCreate(
            _scopeIdentity,
            key,
            valueFactory,
            cancellationToken);
    }

    public ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class, IQueryResultCacheKey
        where TValue : notnull
    {
        ThrowIfInactive();
        return _store.GetOrCreateAsync(
            _scopeIdentity,
            key,
            valueFactory,
            cancellationToken);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _isActive, 0);
    }

    private void ThrowIfInactive()
    {
        if (Volatile.Read(ref _isActive) == 0)
        {
            throw new InvalidOperationException(
                "This query-result cache scope is no longer active because its query invocation has completed.");
        }
    }
}
