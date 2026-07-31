using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCacheEntryIdentity : IEquatable<QueryCacheEntryIdentity>
{
    private readonly object _key;
    private readonly Type _keyType;
    private readonly QueryCacheScopeIdentity _scopeIdentity;
    private readonly Type _valueType;

    public QueryCacheEntryIdentity(
        QueryCacheScopeIdentity scopeIdentity,
        Type keyType,
        Type valueType,
        object key)
    {
        _scopeIdentity = scopeIdentity;
        _keyType = keyType;
        _valueType = valueType;
        _key = key;
    }

    public bool Equals(QueryCacheEntryIdentity? other)
    {
        return other is not null
            && ReferenceEquals(_scopeIdentity.Generation, other._scopeIdentity.Generation)
            && _scopeIdentity.Scope.Equals(other._scopeIdentity.Scope)
            && _keyType == other._keyType
            && _valueType == other._valueType
            && _key.Equals(other._key);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryCacheEntryIdentity);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            RuntimeHelpers.GetHashCode(_scopeIdentity.Generation),
            _scopeIdentity.Scope,
            _keyType,
            _valueType,
            _key);
    }
}
