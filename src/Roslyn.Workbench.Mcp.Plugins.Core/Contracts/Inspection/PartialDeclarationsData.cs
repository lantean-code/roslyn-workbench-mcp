namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

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
