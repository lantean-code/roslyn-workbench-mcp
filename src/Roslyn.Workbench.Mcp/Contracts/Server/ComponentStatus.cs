namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents the status of one runtime component reported by server diagnostics.
/// </summary>
public sealed record ComponentStatus
{
    /// <summary>
    /// Gets a value indicating whether the component is available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Gets the component version, when available.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the optional status message.
    /// </summary>
    public string? Message { get; init; }
}
