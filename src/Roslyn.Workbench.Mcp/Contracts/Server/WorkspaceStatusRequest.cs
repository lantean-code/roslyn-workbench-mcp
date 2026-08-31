namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to retrieve workspace status.
/// </summary>
internal sealed record WorkspaceStatusRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Amount of workspace status detail to return.
    /// </summary>
    [Description("Amount of workspace status detail to return.")]
    public StatusDetailLevel Detail { get; init; } = StatusDetailLevel.Standard;
}
