namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Describes a structured plugin execution failure.
/// </summary>
public sealed record PluginExecutionError
{
    /// <summary>Gets the stable error code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Gets the user-facing error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the optional diagnostic correlation identifier.</summary>
    public string? CorrelationId { get; init; }
}
