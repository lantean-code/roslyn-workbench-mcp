using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

/// <summary>
/// Stores keyed values until they expire while enforcing a fixed capacity.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored values.</typeparam>
/// <typeparam name="TValue">The type of value retained by the store.</typeparam>
internal interface IBoundedExpiringStore<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /// <summary>
    /// Adds a value or replaces the value already associated with the key.
    /// </summary>
    /// <param name="key">The key under which to store the value.</param>
    /// <param name="value">The value to retain.</param>
    void AddOrReplace(TKey key, TValue value);

    /// <summary>
    /// Attempts to add a value without replacing an existing entry.
    /// </summary>
    /// <param name="key">The key under which to store the value.</param>
    /// <param name="value">The value to retain.</param>
    /// <returns><see langword="true"/> when the value was added; otherwise, <see langword="false"/>.</returns>
    bool TryAdd(TKey key, TValue value);

    /// <summary>
    /// Attempts to retrieve the unexpired value associated with a key.
    /// </summary>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">The stored value when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an unexpired value was found; otherwise, <see langword="false"/>.</returns>
    bool TryGet(TKey key, [NotNullWhen(true)] out TValue? value);

    /// <summary>
    /// Replaces an existing unexpired value using an atomic update function.
    /// </summary>
    /// <param name="key">The key of the value to update.</param>
    /// <param name="update">The function that produces a replacement for the stored value.</param>
    /// <returns>A result containing the original and replacement values, or a not-found result when the key has no unexpired value.</returns>
    BoundedExpiringStoreUpdateResult<TValue> Update(
        TKey key,
        Func<TValue, TValue> update);

    /// <summary>
    /// Removes the value associated with a key.
    /// </summary>
    /// <param name="key">The key of the value to remove.</param>
    /// <returns><see langword="true"/> when a value was removed; otherwise, <see langword="false"/>.</returns>
    bool Remove(TKey key);
}
