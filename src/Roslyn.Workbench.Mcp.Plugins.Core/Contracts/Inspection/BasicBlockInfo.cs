namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one basic block in a control-flow-graph projection.
/// </summary>
internal sealed record BasicBlockInfo
{
    /// <summary>
    /// Gets the block ordinal.
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>
    /// Gets the Roslyn block kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the block is reachable.
    /// </summary>
    public bool IsReachable { get; init; }

    /// <summary>
    /// Gets the projected operations in the block.
    /// </summary>
    public IReadOnlyList<string> Operations { get; init; } = [];

    /// <summary>
    /// Gets the fall-through successor ordinal, when available.
    /// </summary>
    public int? FallThroughSuccessor { get; init; }

    /// <summary>
    /// Gets the conditional successor ordinal, when available.
    /// </summary>
    public int? ConditionalSuccessor { get; init; }
}
