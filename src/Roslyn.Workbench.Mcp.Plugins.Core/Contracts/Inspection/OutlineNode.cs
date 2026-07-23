namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one semantic outline node.
/// </summary>
internal sealed record OutlineNode
{
    /// <summary>
    /// Gets the node display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Roslyn symbol or declaration kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional accessibility.
    /// </summary>
    public string? Accessibility { get; init; }

    /// <summary>
    /// Gets the node modifiers.
    /// </summary>
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    /// <summary>
    /// Gets the node location, when available.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the child outline nodes.
    /// </summary>
    public IReadOnlyList<OutlineNode> Children { get; init; } = [];
}
