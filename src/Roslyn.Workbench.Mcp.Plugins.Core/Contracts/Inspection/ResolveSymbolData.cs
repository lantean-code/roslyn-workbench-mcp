namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by resolve-symbol.
/// </summary>
internal sealed record ResolveSymbolData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved symbol.
    /// </summary>
    [Description("The resolved symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the canonical selector for the resolved symbol.
    /// </summary>
    [Description("The canonical selector for the resolved symbol.")]
    public SymbolSelector? Selector { get; init; }

    /// <summary>
    /// Gets the source declarations for the symbol.
    /// </summary>
    [Description("The source declarations for the symbol.")]
    public BoundedCollection<ResolvedLocation> Declarations { get; init; } = BoundedCollection.Empty<ResolvedLocation>();
}
