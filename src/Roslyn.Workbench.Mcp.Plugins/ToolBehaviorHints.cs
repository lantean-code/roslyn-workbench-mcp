namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Describes the behavioural hints published for a tool.
/// </summary>
public sealed record ToolBehaviorHints
{
    /// <summary>
    /// Gets a value indicating whether the tool can replace, remove, or persist source.
    /// </summary>
    public bool Destructive { get; init; }
}
