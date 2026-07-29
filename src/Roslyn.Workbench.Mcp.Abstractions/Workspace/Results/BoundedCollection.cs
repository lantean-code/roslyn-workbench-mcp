using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Results;

#pragma warning disable CA1711 // The bounded-collection wire contract accurately describes its collection payload.
/// <summary>
/// Represents a bounded collection result published to tool consumers.
/// </summary>
/// <typeparam name="TItem">The collection item type.</typeparam>
public sealed record BoundedCollection<TItem>
{
    /// <summary>
    /// Gets the returned items.
    /// </summary>
    public IReadOnlyList<TItem> Items { get; }

    /// <summary>
    /// Gets a value indicating whether more items were available.
    /// </summary>
    public bool HasMore { get; }

    /// <summary>
    /// Gets the complete result count when it was available without additional expensive work.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; }

    internal BoundedCollection(IReadOnlyList<TItem> items, bool hasMore, int? totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (totalCount is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(totalCount.Value, items.Count);
        }

        Items = items;
        HasMore = hasMore;
        TotalCount = totalCount;
    }
}

/// <summary>
/// Creates bounded collection results.
/// </summary>
public static class BoundedCollection
{
    /// <summary>
    /// Gets the shared empty bounded collection for the current item type.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <returns>The shared empty bounded collection.</returns>
    public static BoundedCollection<TItem> Empty<TItem>()
    {
        return Cache<TItem>.Empty;
    }

    /// <summary>
    /// Creates a bounded collection from items that have already been limited by the caller.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <param name="items">The already-limited items to publish.</param>
    /// <param name="hasMore">Whether additional items were available.</param>
    /// <returns>The prebounded collection projection.</returns>
    public static BoundedCollection<TItem> CreatePrebounded<TItem>(
        IReadOnlyList<TItem> items,
        bool hasMore)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0 && !hasMore)
        {
            return Empty<TItem>();
        }

        return new BoundedCollection<TItem>(items, hasMore, hasMore ? null : items.Count);
    }

    /// <summary>
    /// Creates a bounded collection with an authoritative complete result count.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <param name="items">The already-limited items to publish.</param>
    /// <param name="totalCount">The complete result count before the response bound was applied.</param>
    /// <returns>The prebounded collection projection with its complete result count.</returns>
    public static BoundedCollection<TItem> CreatePrebounded<TItem>(
        IReadOnlyList<TItem> items,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalCount, items.Count);

        if (totalCount == 0)
        {
            return Empty<TItem>();
        }

        return new BoundedCollection<TItem>(items, totalCount > items.Count, totalCount);
    }

    private static class Cache<TItem>
    {
        public static BoundedCollection<TItem> Empty { get; } = new(Array.Empty<TItem>(), hasMore: false, totalCount: 0);
    }
}
#pragma warning restore CA1711
