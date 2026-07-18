namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-dependency-graph.
/// </summary>
public sealed record DependencyGraphData
{
    /// <summary>
    /// Gets the returned graph nodes.
    /// </summary>
    public BoundedCollection<GraphNode> Nodes { get; init; } = BoundedCollection<GraphNode>.Empty();

    /// <summary>
    /// Gets the returned graph edges.
    /// </summary>
    public BoundedCollection<GraphEdge> Edges { get; init; } = BoundedCollection<GraphEdge>.Empty();
}
