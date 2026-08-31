namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Owns Workspace query-cache generations, shared computations and lifecycle invalidation.
/// </summary>
internal interface IWorkspaceQueryCacheState
{
    /// <summary>
    /// Creates an entry scope isolated by Workspace, solution snapshot and component.
    /// </summary>
    /// <param name="workspaceId">The Workspace containing the solution.</param>
    /// <param name="solution">The immutable solution snapshot that identifies the generation.</param>
    /// <param name="componentIdentity">The stable identity of the component using the cache.</param>
    /// <returns>An identity that can address entries within the isolated scope.</returns>
    QueryCacheScopeIdentity CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity);

    /// <summary>
    /// Returns a cached value or runs a shared synchronous factory for a missing entry.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="valueFactory">The factory used to create the required value.</param>
    /// <param name="sizeCalculator">Calculates the cache charge for a produced value.</param>
    /// <param name="admissionPredicate">Determines whether a produced value may be retained.</param>
    /// <param name="cancellationToken">Cancels this caller's wait for the value.</param>
    /// <returns>The cached or produced value, or <see langword="null"/> when the factory produces no value.</returns>
    TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <summary>
    /// Returns a cached value or runs a shared asynchronous factory for a missing entry.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="valueFactory">The factory used to create the required value.</param>
    /// <param name="sizeCalculator">Calculates the cache charge for a produced value.</param>
    /// <param name="admissionPredicate">Determines whether a produced value may be retained.</param>
    /// <param name="cancellationToken">Cancels this caller's wait for the value.</param>
    /// <returns>The cached or produced value, or <see langword="null"/> when the factory produces no value.</returns>
    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <summary>
    /// Returns a result that may expose more data than the value retained in the cache.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The retained cache-value type.</typeparam>
    /// <typeparam name="TResult">The caller-facing result type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="resultFactory">Produces the full result when no value is cached.</param>
    /// <param name="cacheValueSelector">Selects the portion of a produced result that is safe to retain.</param>
    /// <param name="cachedResultSelector">Reconstructs a caller-facing result from a retained value.</param>
    /// <param name="sizeCalculator">Calculates the cache charge for the retained value.</param>
    /// <param name="cancellationToken">Cancels this caller's wait for the result.</param>
    /// <returns>The newly produced full result or a result reconstructed from the cached value.</returns>
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

    /// <summary>
    /// Attempts to read a typed entry without running a factory.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="value">Receives the cached value when found.</param>
    /// <returns><see langword="true"/> when the entry exists; otherwise, <see langword="false"/>.</returns>
    bool TryGet<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        out TValue? value)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <summary>
    /// Stores a value when the supplied scope generation remains current.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="value">The value to retain.</param>
    /// <param name="sizeCalculator">Calculates the cache charge for the value.</param>
    void Store<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        TValue value,
        Func<TValue, long> sizeCalculator)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull;

    /// <summary>
    /// Invalidates every query-cache generation associated with a Workspace.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier to invalidate.</param>
    void InvalidateWorkspace(Guid workspaceId);
}
