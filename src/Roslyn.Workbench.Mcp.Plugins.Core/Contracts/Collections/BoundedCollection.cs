namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

#pragma warning disable CA1000, CA1711 // The public bounded-collection contract uses cohesive generic factories and accurately describes its collection payload.
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

        return new BoundedCollection<TItem>
        {
            Items = items,
            HasMore = hasMore,
        };
    }
}
#pragma warning restore CA1000, CA1711
