namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve an operation tree for a selected region.
/// </summary>
public sealed record GetOperationTreeRequest : WorkspaceBoundRequest
{
    internal const int _defaultMaxDepth = 8;

    /// <summary>
    /// Gets the selected location.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the maximum traversal depth.
    /// </summary>
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
