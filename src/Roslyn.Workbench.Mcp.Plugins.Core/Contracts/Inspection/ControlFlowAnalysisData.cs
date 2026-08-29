namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-control-flow.
/// </summary>
internal sealed record ControlFlowAnalysisData : IQueryResponse
{
    /// <summary>
    /// Gets the analyzed region.
    /// </summary>
    [Description("The analyzed region.")]
    public ResolvedLocation? Region { get; init; }

    /// <summary>
    /// Gets a value indicating whether the region entry is reachable.
    /// </summary>
    [Description("Whether the region entry is reachable.")]
    public bool EntryReachable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the region exit is reachable.
    /// </summary>
    [Description("Whether the region exit is reachable.")]
    public bool ExitReachable { get; init; }

    /// <summary>
    /// Gets the projected exit points.
    /// </summary>
    [Description("The projected exit points.")]
    public BoundedCollection<ControlFlowExit> Exits { get; init; } = BoundedCollection.Empty<ControlFlowExit>();

    /// <summary>
    /// Gets the projected return statements.
    /// </summary>
    [Description("The projected return statements.")]
    public BoundedCollection<ResolvedLocation> Returns { get; init; } = BoundedCollection.Empty<ResolvedLocation>();
}
