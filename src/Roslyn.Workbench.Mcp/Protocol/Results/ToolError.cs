using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Represents a structured tool error.
/// </summary>
internal sealed record ToolError
{
    /// <summary>
    /// Stable machine-readable error code.
    /// </summary>
    [Description("Stable machine-readable error code.")]
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable explanation of the failure.
    /// </summary>
    [Description("Human-readable explanation of the failure.")]
    public required string Message { get; init; }

    /// <summary>
    /// Correlation identifier for matching the failure to server-side diagnostics, when available.
    /// </summary>
    [Description("Correlation identifier for matching the failure to server-side diagnostics, when available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }
}
