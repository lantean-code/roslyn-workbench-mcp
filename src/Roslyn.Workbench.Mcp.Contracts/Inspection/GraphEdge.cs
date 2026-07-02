namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one dependency-graph edge.
/// </summary>
public sealed record GraphEdge
{
    /// <summary>
    /// Gets the source node identifier.
    /// </summary>
    public string FromId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source node display name.
    /// </summary>
    public string FromDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target node identifier.
    /// </summary>
    public string ToId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target node display name.
    /// </summary>
    public string ToDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the edge kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;
}
