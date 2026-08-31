using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Identifies query cache entry.
/// </summary>
internal sealed class QueryCacheEntryIdentity : IEquatable<QueryCacheEntryIdentity>
{
    private readonly object _key;
    private readonly Type _keyType;
    private readonly QueryCacheScopeIdentity _scopeIdentity;
    private readonly Type _valueType;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCacheEntryIdentity"/> class.
    /// </summary>
    /// <param name="scopeIdentity">The identity of the workspace scope being processed.</param>
    /// <param name="keyType">The runtime type of keys represented by the cache identity.</param>
    /// <param name="valueType">The runtime type of values represented by the cache identity.</param>
    /// <param name="key">The key used to identify the stored value.</param>
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

    /// <summary>
    /// Determines whether this value equals the supplied value.
    /// </summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns><see langword="true"/> when the condition is satisfied; otherwise, <see langword="false"/>.</returns>
    public bool Equals(QueryCacheEntryIdentity? other)
    {
        return other is not null
            && ReferenceEquals(_scopeIdentity.Generation, other._scopeIdentity.Generation)
            && _scopeIdentity.Scope.Equals(other._scopeIdentity.Scope)
            && _keyType == other._keyType
            && _valueType == other._valueType
            && _key.Equals(other._key);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as QueryCacheEntryIdentity);
    }

    /// <inheritdoc/>
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
