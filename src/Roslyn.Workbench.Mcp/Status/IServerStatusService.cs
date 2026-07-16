using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Status;

internal interface IServerStatusService
{
    ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken);
}
