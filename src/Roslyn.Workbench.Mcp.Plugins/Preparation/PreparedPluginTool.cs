namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

/// <summary>
/// Associates a validated handler type and closed contract with its materialization metadata.
/// </summary>
internal sealed record PreparedPluginTool
{
    /// <summary>
    /// Gets the concrete plugin handler type to create.
    /// </summary>
    public required Type HandlerType { get; init; }

    /// <summary>
    /// Gets the single closed query or mutation handler contract implemented by the handler.
    /// </summary>
    public required Type HandlerContract { get; init; }

    /// <summary>
    /// Gets the validated plugin and transport metadata for the tool.
    /// </summary>
    public required RegisteredTool Tool { get; init; }
}
