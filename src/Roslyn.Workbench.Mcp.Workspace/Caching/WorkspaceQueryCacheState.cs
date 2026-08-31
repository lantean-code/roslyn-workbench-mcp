using System.Runtime.CompilerServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Adapts solution-scoped query operations to the shared generation-aware cache core.
/// </summary>
internal sealed class WorkspaceQueryCacheState : IWorkspaceQueryCacheState, IDisposable
{
    private readonly QueryCacheStateCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceQueryCacheState"/> class.
    /// </summary>
    /// <param name="options">The Workspace cache limits and expiration policy.</param>
    /// <param name="applicationLifetime">The host lifetime whose shutdown signal expires cache entries.</param>
    public WorkspaceQueryCacheState(
        IOptions<WorkspaceQueryCacheOptions> options,
        IHostApplicationLifetime applicationLifetime)
    {
        _core = new QueryCacheStateCore(
            options.Value.SizeLimit,
            options.Value.SlidingExpiration,
            applicationLifetime,
            WorkbenchPerformanceEventSource.WorkspaceQueryCacheFamily);
    }

    /// <summary>
    /// Creates an entry scope isolated by Workspace, solution snapshot, and component.
    /// </summary>
    /// <param name="workspaceId">The Workspace containing the solution.</param>
    /// <param name="solution">The immutable solution snapshot that identifies the generation.</param>
    /// <param name="componentIdentity">The stable identity of the component using the cache.</param>
    /// <returns>An identity that can address entries within the isolated scope.</returns>
    public QueryCacheScopeIdentity CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity)
    {
        var scope = new WorkspaceQueryScope(solution, componentIdentity);
        return _core.CreateScope(workspaceId, scope);
    }

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
        return _core.GetOrCreate(
            scopeIdentity,
            key,
            valueFactory,
            sizeCalculator,
            admissionPredicate,
            cancellationToken);
    }

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
    /// <returns>A task that completes with the cached or produced value, or <see langword="null"/> when the factory produces no value.</returns>
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
        return _core.GetOrCreateAsync(
            scopeIdentity,
            key,
            valueFactory,
            sizeCalculator,
            admissionPredicate,
            cancellationToken);
    }

    /// <summary>
    /// Gets a projected result from the cache or creates, stores, and projects a new value.
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
        return _core.GetOrCreateProjected(
            scopeIdentity,
            key,
            resultFactory,
            cacheValueSelector,
            cachedResultSelector,
            sizeCalculator,
            cancellationToken);
    }

    /// <summary>
    /// Attempts to retrieve a cached value.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="value">Receives the cached value when found.</param>
    /// <returns><see langword="true"/> when the entry exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        out TValue? value)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
    {
        return _core.TryGet(scopeIdentity, key, out value);
    }

    /// <summary>
    /// Stores a value in the cache.
    /// </summary>
    /// <typeparam name="TKey">The Workspace cache-key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="scopeIdentity">The scope that owns the entry.</param>
    /// <param name="key">The key within the scope.</param>
    /// <param name="value">The value to retain.</param>
    /// <param name="sizeCalculator">Calculates the cache charge for the value.</param>
    public void Store<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        TValue value,
        Func<TValue, long> sizeCalculator)
        where TKey : class, IWorkspaceQueryCacheKey
        where TValue : notnull
    {
        _core.Store(scopeIdentity, key, value, sizeCalculator);
    }

    /// <summary>
    /// Invalidates every query-cache generation associated with a Workspace.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier to invalidate.</param>
    public void InvalidateWorkspace(Guid workspaceId)
    {
        _core.InvalidatePartition(workspaceId);
    }

    /// <summary>
    /// Releases resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        _core.Dispose();
    }

    private sealed class WorkspaceQueryScope : IEquatable<WorkspaceQueryScope>
    {
        private readonly string _componentIdentity;
        private readonly Solution _solution;

        public WorkspaceQueryScope(Solution solution, string componentIdentity)
        {
            _solution = solution;
            _componentIdentity = componentIdentity;
        }

        public bool Equals(WorkspaceQueryScope? other)
        {
            return other is not null
                && ReferenceEquals(_solution, other._solution)
                && string.Equals(
                    _componentIdentity,
                    other._componentIdentity,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as WorkspaceQueryScope);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                RuntimeHelpers.GetHashCode(_solution),
                StringComparer.Ordinal.GetHashCode(_componentIdentity));
        }
    }
}
