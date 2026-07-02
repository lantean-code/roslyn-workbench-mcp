using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-dependency-graph.
/// </summary>
public sealed record DependencyGraphData
{
    /// <summary>
    /// Gets the returned graph nodes.
    /// </summary>
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];

    /// <summary>
    /// Gets the returned graph edges.
    /// </summary>
    public IReadOnlyList<GraphEdge> Edges { get; init; } = [];

    /// <summary>
    /// Gets the number of nodes returned.
    /// </summary>
    public int ReturnedNodeCount { get; init; }

    /// <summary>
    /// Gets the number of edges returned.
    /// </summary>
    public int ReturnedEdgeCount { get; init; }

    /// <summary>
    /// Gets the reasons the graph was truncated.
    /// </summary>
    public IReadOnlyList<CollectionTruncation> TruncationReasons { get; init; } = [];
}
