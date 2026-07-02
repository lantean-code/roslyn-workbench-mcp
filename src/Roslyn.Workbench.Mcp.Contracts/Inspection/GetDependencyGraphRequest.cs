using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to return a bounded dependency graph for a selected scope.
/// </summary>
public sealed record GetDependencyGraphRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the scope to graph.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the dependency graph granularity.
    /// </summary>
    public string Granularity { get; init; } = "Type";

    /// <summary>
    /// Gets the maximum traversal depth.
    /// </summary>
    public int MaxDepth { get; init; } = 3;

    /// <summary>
    /// Gets the optional maximum number of nodes to return.
    /// </summary>
    public int? MaxNodes { get; init; }

    /// <summary>
    /// Gets the optional maximum number of edges to return.
    /// </summary>
    public int? MaxEdges { get; init; }
}
