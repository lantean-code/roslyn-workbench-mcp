namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents one dependency-graph edge.
/// </summary>
public sealed record GraphEdge
{
    /// <summary>
    /// Gets the source node identifier.
    /// </summary>
    public required string FromId { get; init; }

    /// <summary>
    /// Gets the source node display name.
    /// </summary>
    public required string FromDisplayName { get; init; }

    /// <summary>
    /// Gets the target node identifier.
    /// </summary>
    public required string ToId { get; init; }

    /// <summary>
    /// Gets the target node display name.
    /// </summary>
    public required string ToDisplayName { get; init; }

    /// <summary>
    /// Gets the edge kind.
    /// </summary>
    public required string Kind { get; init; }
}
