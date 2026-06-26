using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to analyze control flow for a selected region.
/// </summary>
public sealed record AnalyzeControlFlowRequest
{
    /// <summary>
    /// Gets the selected location.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
