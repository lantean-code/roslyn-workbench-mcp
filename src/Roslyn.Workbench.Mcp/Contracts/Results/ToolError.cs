namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Represents a structured tool error.
/// </summary>
internal sealed record ToolError
{
    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional correlation identifier for server-side diagnostics.
    /// </summary>
    public string? CorrelationId { get; init; }
}
