namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class WorkspaceQueryCacheStore : IWorkspaceQueryCacheStore
{
    private readonly IWorkspaceQueryCacheState _state;

    public WorkspaceQueryCacheStore(IWorkspaceQueryCacheState state)
    {
        _state = state;
    }

    public QueryCacheScopeIdentity CreateScope(
        string workspaceId,
        Solution solution,
        string componentIdentity)
    {
        return _state.CreateScope(workspaceId, solution, componentIdentity);
    }

    public TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
    {
        return _state.GetOrCreate(
            scopeIdentity,
            key,
            valueFactory,
            sizeCalculator,
            admissionPredicate,
            cancellationToken);
    }

    public ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
    {
        return _state.GetOrCreateAsync(
            scopeIdentity,
            key,
            valueFactory,
            sizeCalculator,
            admissionPredicate,
            cancellationToken);
    }

    public TResult GetOrCreateProjected<TKey, TValue, TResult>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TResult> resultFactory,
        Func<TResult, TValue?> cacheValueSelector,
        Func<TValue, TResult> cachedResultSelector,
        Func<TValue, long> sizeCalculator,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
        where TResult : notnull
    {
        return _state.GetOrCreateProjected(
            scopeIdentity,
            key,
            resultFactory,
            cacheValueSelector,
            cachedResultSelector,
            sizeCalculator,
            cancellationToken);
    }

    public bool TryGet<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        out TValue? value)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
    {
        return _state.TryGet(scopeIdentity, key, out value);
    }

    public void Store<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        TValue value,
        Func<TValue, long> sizeCalculator)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
    {
        _state.Store(scopeIdentity, key, value, sizeCalculator);
    }
}
