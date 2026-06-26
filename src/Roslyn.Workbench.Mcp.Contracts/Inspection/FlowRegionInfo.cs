namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one flow region in a control-flow-graph projection.
/// </summary>
public sealed record FlowRegionInfo
{
    /// <summary>
    /// Gets the region identifier.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the region kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the first block ordinal in the region.
    /// </summary>
    public int FirstBlockOrdinal { get; init; }

    /// <summary>
    /// Gets the last block ordinal in the region.
    /// </summary>
    public int LastBlockOrdinal { get; init; }
}
