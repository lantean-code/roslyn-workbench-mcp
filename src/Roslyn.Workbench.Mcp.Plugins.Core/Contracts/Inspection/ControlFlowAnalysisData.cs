namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-control-flow.
/// </summary>
internal sealed record ControlFlowAnalysisData
{
    /// <summary>
    /// Gets the analyzed region.
    /// </summary>
    public ResolvedLocation? Region { get; init; }

    /// <summary>
    /// Gets a value indicating whether the region entry is reachable.
    /// </summary>
    public bool EntryReachable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the region exit is reachable.
    /// </summary>
    public bool ExitReachable { get; init; }

    /// <summary>
    /// Gets the projected exit points.
    /// </summary>
    public IReadOnlyList<ControlFlowExit> Exits { get; init; } = [];

    /// <summary>
    /// Gets the projected return statements.
    /// </summary>
    public IReadOnlyList<ResolvedLocation> Returns { get; init; } = [];
}
