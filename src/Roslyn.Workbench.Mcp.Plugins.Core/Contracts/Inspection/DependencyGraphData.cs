namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-dependency-graph.
/// </summary>
internal sealed record DependencyGraphData : IQueryResponse
{
    /// <summary>
    /// Gets the returned graph nodes.
    /// </summary>
    [Description("The returned graph nodes.")]
    public BoundedCollection<GraphNode> Nodes { get; init; } = BoundedCollection.Empty<GraphNode>();

    /// <summary>
    /// Gets the returned graph edges.
    /// </summary>
    [Description("The returned graph edges.")]
    public BoundedCollection<GraphEdge> Edges { get; init; } = BoundedCollection.Empty<GraphEdge>();
}
