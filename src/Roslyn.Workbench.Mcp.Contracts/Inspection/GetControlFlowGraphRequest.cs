using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve a control-flow graph for a symbol or location.
/// </summary>
public sealed record GetControlFlowGraphRequest
{
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
