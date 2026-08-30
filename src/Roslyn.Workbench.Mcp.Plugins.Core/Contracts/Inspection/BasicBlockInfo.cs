namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one basic block in a control-flow-graph projection.
/// </summary>
internal sealed record BasicBlockInfo
{
    /// <summary>
    /// Gets the block ordinal.
    /// </summary>
    [Description("The block ordinal.")]
    public int Ordinal { get; init; }

    /// <summary>
    /// Gets the Roslyn block kind.
    /// </summary>
    [Description("The Roslyn block kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the block is reachable.
    /// </summary>
    [Description("Whether the block is reachable.")]
    public bool IsReachable { get; init; }

    /// <summary>
    /// Gets the projected operations in the block.
    /// </summary>
    [Description("The projected operations in the block.")]
    public BoundedCollection<BasicBlockOperationInfo> Operations { get; init; } = BoundedCollection.Empty<BasicBlockOperationInfo>();

    /// <summary>
    /// Gets the fall-through successor ordinal, when available.
    /// </summary>
    [Description("The fall-through successor ordinal, when available.")]
    public int? FallThroughSuccessor { get; init; }

    /// <summary>
    /// Gets the conditional successor ordinal, when available.
    /// </summary>
    [Description("The conditional successor ordinal, when available.")]
    public int? ConditionalSuccessor { get; init; }
}
