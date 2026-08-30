namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one semantic outline node.
/// </summary>
internal sealed record OutlineNode
{
    /// <summary>
    /// Gets the node display name.
    /// </summary>
    [Description("The node display name.")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the Roslyn symbol or declaration kind.
    /// </summary>
    [Description("The Roslyn symbol or declaration kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the optional accessibility.
    /// </summary>
    [Description("The optional accessibility.")]
    public string? Accessibility { get; init; }

    /// <summary>
    /// Gets the node modifiers.
    /// </summary>
    [Description("The node modifiers.")]
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    /// <summary>
    /// Gets the node location, when available.
    /// </summary>
    [Description("The node location, when available.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the child outline nodes.
    /// </summary>
    [Description("The child outline nodes.")]
    public IReadOnlyList<OutlineNode> Children { get; init; } = [];
}
