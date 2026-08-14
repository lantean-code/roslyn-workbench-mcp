namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to analyze data flow for a selected region.
/// </summary>
internal sealed record AnalyzeDataFlowRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets an exact expression, complete statement or contiguous statement range to analyze.
    /// </summary>
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
