using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents a collection query response.
/// </summary>
/// <typeparam name="TItem">The published item type.</typeparam>
public sealed record CollectionResponse<TItem> : QueryResponse
{
    /// <summary>
    /// Gets the published items.
    /// </summary>
    public IReadOnlyList<TItem> Items { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether more items were available than were returned.
    /// </summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// Gets the optional truncation reasons.
    /// </summary>
    public IReadOnlyList<CollectionTruncation>? TruncatedBy { get; init; }
}
