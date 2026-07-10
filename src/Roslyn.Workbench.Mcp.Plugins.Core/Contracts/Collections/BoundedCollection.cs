namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

/// <summary>
/// Represents a bounded collection result published to tool consumers.
/// </summary>
/// <typeparam name="TItem">The collection item type.</typeparam>
public sealed record BoundedCollection<TItem>
{
    private static readonly BoundedCollection<TItem> _empty = new()
    {
        Items = Array.Empty<TItem>(),
    };

    /// <summary>
    /// Gets the returned items.
    /// </summary>
    public IReadOnlyList<TItem> Items { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether more items were available.
    /// </summary>
    public bool HasMore { get; init; }

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

        return new BoundedCollection<TItem>
        {
            Items = items,
        };
    }

    /// <summary>
    /// Creates a bounded collection from an ordered source set.
    /// </summary>
    /// <param name="orderedItems">The ordered items to bound.</param>
    /// <param name="maxResults">The maximum number of items to return.</param>
    /// <returns>The bounded collection projection.</returns>
    public static BoundedCollection<TItem> Create(
        IReadOnlyList<TItem> orderedItems,
        int maxResults)
    {
        ArgumentNullException.ThrowIfNull(orderedItems);

        var limitedItems = orderedItems.Count > maxResults
            ? orderedItems.Take(maxResults).ToArray()
            : orderedItems;
        var hasMore = orderedItems.Count > limitedItems.Count;
        if (!hasMore && limitedItems.Count == 0)
        {
            return Empty();
        }

        return new BoundedCollection<TItem>
        {
            Items = limitedItems,
            HasMore = hasMore,
        };
    }
}
