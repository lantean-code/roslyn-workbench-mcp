namespace Roslyn.Workbench.Mcp.Plugins.Registration;

/// <summary>
/// Describes one tool registration supplied by a plugin.
/// </summary>
internal sealed record ToolRegistrationMetadata
{
    /// <summary>
    /// Gets the globally unique MCP tool name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tool title displayed to users.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tool description displayed to users.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional concise result summary displayed to users.
    /// </summary>
    public string? ResultSummary { get; init; }

    /// <summary>
    /// Gets the published behavioural hints for the tool.
    /// </summary>
    public ToolBehaviorHints Behavior { get; init; } = new();
}
