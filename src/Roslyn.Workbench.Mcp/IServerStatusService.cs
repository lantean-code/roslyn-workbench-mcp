using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp;

internal interface IServerStatusService
{
    ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken);
}
