namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents a request to retrieve server diagnostics.
/// </summary>
public sealed record ServerStatusRequest
{
    /// <summary>
    /// Gets the requested response detail level.
    /// </summary>
    public StatusDetailLevel Detail { get; init; } = StatusDetailLevel.Minimal;
}
