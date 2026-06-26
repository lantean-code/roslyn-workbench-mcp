using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-control-flow-graph.
/// </summary>
public sealed record ControlFlowGraphData
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
    /// Gets the projected flow regions.
    /// </summary>
    public IReadOnlyList<FlowRegionInfo> Regions { get; init; } = [];
}
