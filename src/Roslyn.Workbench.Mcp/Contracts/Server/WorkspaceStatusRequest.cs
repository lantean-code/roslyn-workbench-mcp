namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to retrieve workspace status.
/// </summary>
internal sealed record WorkspaceStatusRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the requested response detail level.
    /// </summary>
    public StatusDetailLevel Detail { get; init; } = StatusDetailLevel.Standard;
}
