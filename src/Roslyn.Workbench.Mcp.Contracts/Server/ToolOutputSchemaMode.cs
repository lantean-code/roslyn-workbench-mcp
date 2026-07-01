using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Controls how MCP tool output schemas are published in tool metadata.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolOutputSchemaMode>))]
public enum ToolOutputSchemaMode
{
    /// <summary>
    /// Omits the published output schema from the tool metadata.
    /// </summary>
    Omit = 0,

    /// <summary>
    /// Publishes the full generated output schema in the tool metadata.
    /// </summary>
    Full = 1,
}
