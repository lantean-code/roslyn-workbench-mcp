using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Represents a structured tool error.
/// </summary>
internal sealed record ToolError
{
    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the optional correlation identifier for server-side diagnostics.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }
}
