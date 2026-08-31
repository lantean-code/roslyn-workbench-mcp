namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

/// <summary>
/// Captures a handler type and its mutable configuration builder until plugin configuration is frozen.
/// </summary>
internal sealed record ConfiguredToolDefinition
{
    /// <summary>
    /// Gets the plugin handler type being configured.
    /// </summary>
    public required Type HandlerType { get; init; }

    /// <summary>
    /// Gets the query or mutation family assigned to the handler.
    /// </summary>
    public required ToolKind Kind { get; init; }

    /// <summary>
    /// Gets the builder that owns the handler's configured metadata.
    /// </summary>
    public required IToolConfigurationBuilderState Builder { get; init; }
}
