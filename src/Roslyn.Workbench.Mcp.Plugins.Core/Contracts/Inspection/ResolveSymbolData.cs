namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by resolve-symbol.
/// </summary>
internal sealed record ResolveSymbolData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the canonical selector for the resolved symbol.
    /// </summary>
    public SymbolSelector? Selector { get; init; }

    /// <summary>
    /// Gets the source declarations for the symbol.
    /// </summary>
    public BoundedCollection<ResolvedLocation> Declarations { get; init; } = BoundedCollection.Empty<ResolvedLocation>();
}
