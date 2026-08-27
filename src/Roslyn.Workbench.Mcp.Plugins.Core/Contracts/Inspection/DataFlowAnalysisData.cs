namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-data-flow.
/// </summary>
internal sealed record DataFlowAnalysisData : IQueryResponse
{
    /// <summary>
    /// Gets the analyzed region.
    /// </summary>
    public ResolvedLocation? Region { get; init; }

    /// <summary>
    /// Gets the variables declared within the region.
    /// </summary>
    public BoundedCollection<SymbolReference> VariablesDeclared { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the symbols read within the region.
    /// </summary>
    public BoundedCollection<SymbolReference> ReadInside { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the symbols written within the region.
    /// </summary>
    public BoundedCollection<SymbolReference> WrittenInside { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the symbols flowing into the region.
    /// </summary>
    public BoundedCollection<SymbolReference> DataFlowsIn { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the symbols flowing out of the region.
    /// </summary>
    public BoundedCollection<SymbolReference> DataFlowsOut { get; init; } = BoundedCollection.Empty<SymbolReference>();

    /// <summary>
    /// Gets the captured symbols.
    /// </summary>
    public BoundedCollection<SymbolReference> Captured { get; init; } = BoundedCollection.Empty<SymbolReference>();
}
