using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-partial-declarations.
/// </summary>
[PublishedCollectionResponse(nameof(Declarations))]
public sealed record PartialDeclarationsData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved declarations.
    /// </summary>
    public IReadOnlyList<ResolvedLocation> Declarations { get; init; } = [];

    /// <summary>
    /// Gets the number of declarations returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more declarations were available.
    /// </summary>
    public bool HasMore { get; init; }
}
