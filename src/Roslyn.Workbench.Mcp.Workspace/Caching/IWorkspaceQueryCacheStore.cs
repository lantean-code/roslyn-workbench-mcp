namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Provides query services with scoped access to the shared Workspace query-cache state.
/// </summary>
internal interface IWorkspaceQueryCacheStore
{
    /// <inheritdoc cref="IWorkspaceQueryCacheState.CreateScope(Guid, Solution, string)"/>
    QueryCacheScopeIdentity CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity);

    /// <inheritdoc cref="IWorkspaceQueryCacheState.GetOrCreate{TKey, TValue}(QueryCacheScopeIdentity, TKey, Func{CancellationToken, TValue}, Func{TValue, long}, Func{TValue, bool}, CancellationToken)"/>
    TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <inheritdoc cref="IWorkspaceQueryCacheState.GetOrCreateAsync{TKey, TValue}(QueryCacheScopeIdentity, TKey, Func{CancellationToken, ValueTask{TValue}}, Func{TValue, long}, Func{TValue, bool}, CancellationToken)"/>
    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <inheritdoc cref="IWorkspaceQueryCacheState.GetOrCreateProjected{TKey, TValue, TResult}(QueryCacheScopeIdentity, TKey, Func{CancellationToken, TResult}, Func{TResult, TValue}, Func{TValue, TResult}, Func{TValue, long}, CancellationToken)"/>
    TResult GetOrCreateProjected<TKey, TValue, TResult>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TResult> resultFactory,
        Func<TResult, TValue?> cacheValueSelector,
        Func<TValue, TResult> cachedResultSelector,
        Func<TValue, long> sizeCalculator,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
        where TResult : notnull;

    /// <inheritdoc cref="IWorkspaceQueryCacheState.TryGet{TKey, TValue}(QueryCacheScopeIdentity, TKey, out TValue)"/>
    bool TryGet<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        out TValue? value)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <inheritdoc cref="IWorkspaceQueryCacheState.Store{TKey, TValue}(QueryCacheScopeIdentity, TKey, TValue, Func{TValue, long})"/>
    void Store<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        TValue value,
        Func<TValue, long> sizeCalculator)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;
}
