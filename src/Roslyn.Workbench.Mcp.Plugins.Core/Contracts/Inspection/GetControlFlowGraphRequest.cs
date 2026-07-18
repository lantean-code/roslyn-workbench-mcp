namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve a control-flow graph for a symbol or location.
/// </summary>
public sealed record GetControlFlowGraphRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the maximum number of projected basic blocks.
    /// </summary>
    public int MaxBlocks { get; init; } = 64;

    /// <summary>
    /// Gets the maximum number of projected flow regions.
    /// </summary>
    public int MaxRegions { get; init; } = 32;

    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
