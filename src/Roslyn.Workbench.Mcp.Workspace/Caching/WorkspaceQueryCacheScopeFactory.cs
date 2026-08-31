namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Creates component-facing cache scopes over the shared Workspace query-cache store.
/// </summary>
internal sealed class WorkspaceQueryCacheScopeFactory : IWorkspaceQueryCacheScopeFactory
{
    private readonly IWorkspaceQueryCacheStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceQueryCacheScopeFactory"/> class.
    /// </summary>
    /// <param name="store">The store that owns cache entry operations.</param>
    public WorkspaceQueryCacheScopeFactory(IWorkspaceQueryCacheStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public IWorkspaceQueryCacheScope CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity)
    {
        var scopeIdentity = _store.CreateScope(
            workspaceId,
            solution,
            componentIdentity);

        return new WorkspaceQueryCacheScope(_store, scopeIdentity);
    }

    private sealed class WorkspaceQueryCacheScope : IWorkspaceQueryCacheScope
    {
        private readonly QueryCacheScopeIdentity _scopeIdentity;
        private readonly IWorkspaceQueryCacheStore _store;

        public WorkspaceQueryCacheScope(
            IWorkspaceQueryCacheStore store,
            QueryCacheScopeIdentity scopeIdentity)
        {
            _store = store;
            _scopeIdentity = scopeIdentity;
        }

        public TValue? GetOrCreate<TKey, TValue>(
            TKey key,
            Func<CancellationToken, TValue?> valueFactory,
            Func<TValue, long> sizeCalculator,
            Func<TValue, bool> admissionPredicate,
            CancellationToken cancellationToken)
            where TKey : class, IWorkspaceQueryCacheKey
            where TValue : notnull
        {
            return _store.GetOrCreate(
                _scopeIdentity,
                key,
                valueFactory,
                sizeCalculator,
                admissionPredicate,
                cancellationToken);
        }

        public ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
            TKey key,
            Func<CancellationToken, ValueTask<TValue?>> valueFactory,
            Func<TValue, long> sizeCalculator,
            Func<TValue, bool> admissionPredicate,
            CancellationToken cancellationToken)
            where TKey : class, IWorkspaceQueryCacheKey
            where TValue : notnull
        {
            return _store.GetOrCreateAsync(
                _scopeIdentity,
                key,
                valueFactory,
                sizeCalculator,
                admissionPredicate,
                cancellationToken);
        }

        public TResult GetOrCreateProjected<TKey, TValue, TResult>(
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
            return _store.GetOrCreateProjected(
                _scopeIdentity,
                key,
                resultFactory,
                cacheValueSelector,
                cachedResultSelector,
                sizeCalculator,
                cancellationToken);
        }

        public bool TryGet<TKey, TValue>(TKey key, out TValue? value)
            where TKey : class, IWorkspaceQueryCacheKey
            where TValue : notnull
        {
            return _store.TryGet(_scopeIdentity, key, out value);
        }

        public void Store<TKey, TValue>(
            TKey key,
            TValue value,
            Func<TValue, long> sizeCalculator)
            where TKey : class, IWorkspaceQueryCacheKey
            where TValue : notnull
        {
            _store.Store(_scopeIdentity, key, value, sizeCalculator);
        }
    }
}
