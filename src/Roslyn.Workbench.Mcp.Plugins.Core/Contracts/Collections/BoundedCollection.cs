using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

#pragma warning disable CA1000, CA1711 // The public bounded-collection contract uses cohesive generic factories and accurately describes its collection payload.
/// <summary>
/// Represents a bounded collection result published to tool consumers.
/// </summary>
/// <typeparam name="TItem">The collection item type.</typeparam>
public sealed record BoundedCollection<TItem>
{
    private static readonly BoundedCollection<TItem> _empty = new(Array.Empty<TItem>(), hasMore: false, totalCount: 0);

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

    private BoundedCollection(IReadOnlyList<TItem> items, bool hasMore, int? totalCount)
    {
        Items = items;
        HasMore = hasMore;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Gets the shared empty bounded collection for the current item type.
    /// </summary>
    /// <returns>The shared empty bounded collection.</returns>
    public static BoundedCollection<TItem> Empty()
    {
        return _empty;
    }

    /// <summary>
    /// Creates an untruncated bounded collection from the supplied items.
    /// </summary>
    /// <param name="items">The items to publish.</param>
    /// <returns>The untruncated bounded collection projection.</returns>
    public static BoundedCollection<TItem> Create(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return Empty();
        }

        return new BoundedCollection<TItem>(items, hasMore: false, totalCount: items.Count);
    }

    /// <summary>
    /// Creates a bounded collection from items that have already been limited by the caller.
    /// </summary>
    /// <param name="items">The already-limited items to publish.</param>
    /// <param name="hasMore">Whether additional items were available.</param>
    /// <returns>The prebounded collection projection.</returns>
    public static BoundedCollection<TItem> CreatePrebounded(
        IReadOnlyList<TItem> items,
        bool hasMore)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0 && !hasMore)
        {
            return Empty();
        }

        return new BoundedCollection<TItem>(items, hasMore, hasMore ? null : items.Count);
    }

    /// <summary>
    /// Creates a bounded collection with an authoritative complete result count.
    /// </summary>
    /// <param name="items">The already-limited items to publish.</param>
    /// <param name="totalCount">The complete result count before the response bound was applied.</param>
    /// <returns>The prebounded collection projection with its complete result count.</returns>
    public static BoundedCollection<TItem> CreatePrebounded(
        IReadOnlyList<TItem> items,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalCount, items.Count);

        if (totalCount == 0)
        {
            return Empty();
        }

        return new BoundedCollection<TItem>(items, totalCount > items.Count, totalCount);
    }
}
#pragma warning restore CA1000, CA1711
