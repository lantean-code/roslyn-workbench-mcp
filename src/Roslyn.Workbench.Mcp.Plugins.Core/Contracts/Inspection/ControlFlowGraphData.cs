namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-control-flow-graph.
/// </summary>
internal sealed record ControlFlowGraphData : IQueryResponse
{
    /// <summary>
    /// Gets the owning callable symbol.
    /// </summary>
    public SymbolReference? Owner { get; init; }

    /// <summary>
    /// Gets the projected basic blocks.
    /// </summary>
    public BoundedCollection<BasicBlockInfo> Blocks { get; init; } = BoundedCollection.Empty<BasicBlockInfo>();

    /// <summary>
    /// Gets the projected flow regions.
    /// </summary>
    public BoundedCollection<FlowRegionInfo> Regions { get; init; } = BoundedCollection.Empty<FlowRegionInfo>();
}
