namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents one detected dependency cycle.
/// </summary>
public sealed record DependencyCycle
{
    /// <summary>
    /// Gets the nodes participating in the cycle.
    /// </summary>
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];
}
