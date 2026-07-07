using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-partial-declarations.
/// </summary>
public sealed record PartialDeclarationsData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved declarations.
    /// </summary>
    public BoundedCollection<ResolvedLocation> Declarations { get; init; } = BoundedCollection<ResolvedLocation>.Empty();
}
