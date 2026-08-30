namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents one dependency-graph node.
/// </summary>
public sealed record GraphNode
{
    /// <summary>
    /// Gets the stable node identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the graph node kind.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the node display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the optional backing symbol when the node maps to a source symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }
}
