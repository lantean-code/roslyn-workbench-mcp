namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve an operation tree for a selected region.
/// </summary>
internal sealed record GetOperationTreeRequest : WorkspaceBoundRequest
{
    private const int _defaultMaxDepth = 8;
    private const int _defaultNodesMaxResults = 200;
    private const int _maximumMaxDepth = 24;
    private const int _maximumNodesMaxResults = 2_000;

    /// <summary>
    /// Gets the selected location.
    /// </summary>
    [Description("The selected location.")]
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the maximum traversal depth.
    /// </summary>
    [Description("The maximum traversal depth.")]
    [Range(0, _maximumMaxDepth)]
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional maximum total number of projected operation nodes.
    /// </summary>
    [Description("The optional maximum total number of projected operation nodes.")]
    [Range(0, _maximumNodesMaxResults)]
    [DefaultValue(_defaultNodesMaxResults)]
    public int? NodesLimit { get; init; } = _defaultNodesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective nodes limit.
    /// </summary>
    internal int EffectiveNodesLimit => ResultLimit.GetEffectiveValue(NodesLimit, _defaultNodesMaxResults);
}
