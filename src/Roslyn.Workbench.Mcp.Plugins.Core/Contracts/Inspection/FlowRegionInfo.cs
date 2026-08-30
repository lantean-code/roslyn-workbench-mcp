namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one flow region in a control-flow-graph projection.
/// </summary>
internal sealed record FlowRegionInfo
{
    /// <summary>
    /// Gets the region identifier.
    /// </summary>
    [Description("The region identifier.")]
    public int Id { get; init; }

    /// <summary>
    /// Gets the region kind.
    /// </summary>
    [Description("The region kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the first block ordinal in the region.
    /// </summary>
    [Description("The first block ordinal in the region.")]
    public int FirstBlockOrdinal { get; init; }

    /// <summary>
    /// Gets the last block ordinal in the region.
    /// </summary>
    [Description("The last block ordinal in the region.")]
    public int LastBlockOrdinal { get; init; }
}
