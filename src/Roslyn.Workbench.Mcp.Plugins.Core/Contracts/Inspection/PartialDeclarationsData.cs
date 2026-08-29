namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-partial-declarations.
/// </summary>
internal sealed record PartialDeclarationsData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved declarations.
    /// </summary>
    [Description("The resolved declarations.")]
    public BoundedCollection<ResolvedLocation> Declarations { get; init; } = BoundedCollection.Empty<ResolvedLocation>();
}
