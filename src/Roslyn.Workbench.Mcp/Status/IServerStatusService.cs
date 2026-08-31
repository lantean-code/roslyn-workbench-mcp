namespace Roslyn.Workbench.Mcp.Status;

/// <summary>
/// Builds server-status responses from current host configuration and component state.
/// </summary>
internal interface IServerStatusService
{
    /// <summary>
    /// Gets the current status at the requested level of detail.
    /// </summary>
    /// <param name="detail">The requested level of detail for the server status response.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing the server status response.</returns>
    ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken);
}
