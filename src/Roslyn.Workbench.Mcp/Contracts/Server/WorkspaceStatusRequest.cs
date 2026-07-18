namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents a request to retrieve workspace status.
/// </summary>
public sealed record WorkspaceStatusRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the requested response detail level.
    /// </summary>
    public StatusDetailLevel Detail { get; init; } = StatusDetailLevel.Standard;
}
