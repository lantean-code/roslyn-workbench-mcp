using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCache : IQueryCache
{
    private readonly IMemoryCache _cache;
    private readonly QueryCacheOptions _options;
    private readonly IQueryCacheInvalidationTokenSource _invalidationTokenSource;

    public QueryCache(
        IMemoryCache cache,
        IOptions<QueryCacheOptions> options,
        IQueryCacheInvalidationTokenSource invalidationTokenSource)
    {
        _cache = cache;
        _options = options.Value;
        _invalidationTokenSource = invalidationTokenSource;
    }

    public bool TryGet<TValue>(string workspaceId, object key, [NotNullWhen(true)] out TValue? value)
        where TValue : class
    {
        return _cache.TryGetValue(new QueryCacheKey(workspaceId, key), out value);
    }

    public void Store<TValue>(string workspaceId, object key, TValue value, long size)
        where TValue : class
    {
        if (size <= 0 || size > _options.SizeLimit)
        {
            return;
        }

        var options = new MemoryCacheEntryOptions()
            .SetSize(size)
            .SetSlidingExpiration(_options.SlidingExpiration)
            .AddExpirationToken(_invalidationTokenSource.GetInvalidationToken(workspaceId));

        _cache.Set(new QueryCacheKey(workspaceId, key), value, options);
    }

    private sealed class QueryCacheKey : IEquatable<QueryCacheKey>
    {
        private readonly object _key;
        private readonly string _workspaceId;

        public QueryCacheKey(string workspaceId, object key)
        {
            _workspaceId = workspaceId;
            _key = key;
        }

        public bool Equals(QueryCacheKey? other)
        {
            return other is not null
                && string.Equals(_workspaceId, other._workspaceId, StringComparison.Ordinal)
                && _key.Equals(other._key);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as QueryCacheKey);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(_workspaceId), _key);
        }
    }
}
