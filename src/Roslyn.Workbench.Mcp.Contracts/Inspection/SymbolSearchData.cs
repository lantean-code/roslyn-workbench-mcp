using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by search-symbols.
/// </summary>
[PublishedCollectionResponse(nameof(Symbols))]
public sealed record SymbolSearchData
{
    /// <summary>
    /// Gets the returned symbols.
    /// </summary>
    public IReadOnlyList<SymbolReference> Symbols { get; init; } = [];

    /// <summary>
    /// Gets the number of symbols returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more symbols were available.
    /// </summary>
    public bool HasMore { get; init; }
}
