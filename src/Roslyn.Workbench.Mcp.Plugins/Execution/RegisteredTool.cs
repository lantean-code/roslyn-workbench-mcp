using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Represents one validated plugin tool that can be exposed at the MCP boundary.
/// </summary>
public sealed record RegisteredTool
{
    /// <summary>
    /// Gets the owning plugin metadata.
    /// </summary>
    public PluginMetadata Plugin { get; init; } = new();

    /// <summary>
    /// Gets the registered tool metadata.
    /// </summary>
    public ToolRegistrationMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Gets the tool kind.
    /// </summary>
    public ToolKind Kind { get; init; }

    /// <summary>
    /// Gets the request contract type.
    /// </summary>
    public Type RequestType { get; init; } = typeof(object);

    /// <summary>
    /// Gets the generated MCP input schema.
    /// </summary>
    public JsonElement InputSchema { get; init; }

    /// <summary>
    /// Gets the generated MCP output schema.
    /// </summary>
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// Gets the generated MCP tool annotations.
    /// </summary>
    public ToolAnnotations Annotations { get; init; } = new();
}
