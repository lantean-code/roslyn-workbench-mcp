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
    public IReadOnlyList<BasicBlockInfo> Blocks { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the basic block list was truncated.
    /// </summary>
    public bool BlocksTruncated { get; init; }

    /// <summary>
    /// Gets the projected flow regions.
    /// </summary>
    public IReadOnlyList<FlowRegionInfo> Regions { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the flow region list was truncated.
    /// </summary>
    public bool RegionsTruncated { get; init; }
}
