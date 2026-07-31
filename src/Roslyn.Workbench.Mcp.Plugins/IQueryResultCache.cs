namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Provides invocation-bound access to reusable plugin query results.
/// </summary>
public interface IQueryResultCache
{
    /// <summary>
    /// Gets an existing result or synchronously creates one for the current query scope.
    /// </summary>
    /// <typeparam name="TKey">The dedicated semantic key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="key">The semantic key.</param>
    /// <param name="valueFactory">The factory used on a cache miss.</param>
    /// <param name="cancellationToken">Cancels only this caller's wait for the shared computation.</param>
    /// <returns>The existing or computed value. A <see langword="null" /> result is returned but not retained.</returns>
    TValue? GetOrCreate<TKey, TValue>(
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class, IQueryResultCacheKey
        where TValue : notnull;

    /// <summary>
    /// Gets an existing result or asynchronously creates one for the current query scope.
    /// </summary>
    /// <typeparam name="TKey">The dedicated semantic key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="key">The semantic key.</param>
    /// <param name="valueFactory">The factory used on a cache miss.</param>
    /// <param name="cancellationToken">Cancels only this caller's wait for the shared computation.</param>
    /// <returns>The existing or computed value. A <see langword="null" /> result is returned but not retained.</returns>
    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class, IQueryResultCacheKey
        where TValue : notnull;
}
