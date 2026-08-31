using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

/// <summary>
/// Defines the capacity, expiration and eviction rules for a bounded expiring store.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored values.</typeparam>
/// <typeparam name="TValue">The type of value retained by the store.</typeparam>
internal interface IBoundedExpiringStorePolicy<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /// <summary>
    /// Gets the maximum number of values retained by the store.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Determines when a value expires.
    /// </summary>
    /// <param name="value">The stored value whose expiration is required.</param>
    /// <returns>The absolute time at which the value expires.</returns>
    DateTimeOffset GetExpiration(TValue value);

    /// <summary>
    /// Selects an existing entry to evict when the store has reached capacity.
    /// </summary>
    /// <param name="entries">The current unexpired entries.</param>
    /// <param name="key">The key selected for eviction when one can be chosen.</param>
    /// <returns><see langword="true"/> when an entry may be evicted; otherwise, <see langword="false"/>.</returns>
    bool TrySelectEvictionKey(
        IReadOnlyDictionary<TKey, TValue> entries,
        [MaybeNullWhen(false)] out TKey key);
}
