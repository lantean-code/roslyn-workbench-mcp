namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-implementations.
/// </summary>
internal sealed record ImplementationSearchData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved implementations.
    /// </summary>
    [Description("The resolved implementations.")]
    public BoundedCollection<SymbolReference> Implementations { get; init; } = BoundedCollection.Empty<SymbolReference>();
}
