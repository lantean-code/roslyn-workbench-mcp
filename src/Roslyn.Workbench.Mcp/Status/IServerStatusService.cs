namespace Roslyn.Workbench.Mcp.Status;

internal interface IServerStatusService
{
    ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken);
}
