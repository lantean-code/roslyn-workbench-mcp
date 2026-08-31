namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Carries the plugin identity, published metadata and CLR contract types for a materialized tool.
/// </summary>
internal sealed record RegisteredTool
{
    /// <summary>
    /// Gets the metadata of the plugin that owns the tool.
    /// </summary>
    public required PluginMetadata Plugin { get; init; }

    /// <summary>
    /// Gets the validated metadata published for the tool.
    /// </summary>
    public required ToolRegistrationMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the query or mutation family represented by the registration.
    /// </summary>
    public ToolKind Kind { get; init; }

    /// <summary>
    /// Gets the request contract type accepted by the handler.
    /// </summary>
    public Type RequestType { get; init; } = typeof(object);

    /// <summary>
    /// Gets the response contract type, or <see cref="object"/> for mutation tools.
    /// </summary>
    public Type ResponseType { get; init; } = typeof(object);
}
