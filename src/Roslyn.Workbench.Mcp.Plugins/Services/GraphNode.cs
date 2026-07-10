using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents one dependency-graph node.
/// </summary>
public sealed record GraphNode
{
    /// <summary>
    /// Gets the stable node identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the graph node kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the node display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional backing symbol when the node maps to a source symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }
}
