namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to retrieve server diagnostics.
/// </summary>
internal sealed record ServerStatusRequest
{
    /// <summary>
    /// Gets the requested response detail level.
    /// </summary>
    [Description("Amount of server status detail to return.")]
    public StatusDetailLevel Detail { get; init; } = StatusDetailLevel.Minimal;
}
