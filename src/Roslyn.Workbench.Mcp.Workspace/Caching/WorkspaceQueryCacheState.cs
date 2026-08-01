using System.Runtime.CompilerServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class WorkspaceQueryCacheState : IWorkspaceQueryCacheState, IDisposable
{
    private readonly QueryCacheStateCore _core;

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

    public QueryCacheScopeIdentity CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity)
    {
        var scope = new WorkspaceQueryScope(solution, componentIdentity);
        return _core.CreateScope(workspaceId, scope);
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
        return _core.GetOrCreate(
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
        return _core.GetOrCreateAsync(
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
        return _core.GetOrCreateProjected(
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
        return _core.TryGet(scopeIdentity, key, out value);
    }

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

    public void InvalidateWorkspace(Guid workspaceId)
    {
        _core.InvalidatePartition(workspaceId);
    }

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
