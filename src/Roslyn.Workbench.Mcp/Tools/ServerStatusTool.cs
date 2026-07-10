using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class ServerStatusTool : ServerOwnedToolBase<ServerStatusRequest, ServerStatusData>
{
    private readonly IServerStatusService _serverStatusService;

    public ServerStatusTool(
        IOptions<StartupOptions> startupOptions,
        IServerStatusService serverStatusService)
        : base(
            startupOptions: startupOptions,
            name: "server-status",
            title: "Server Status",
            description: "Returns server diagnostics without requiring a loaded workspace.",
            readOnly: true,
            destructive: false,
            resultSummary: "server diagnostics, effective configuration, plugin status, and unfinished recovery state.")
    {
        _serverStatusService = serverStatusService;
    }

    protected override ValueTask<ToolResult<ServerStatusData>> ExecuteAsync(
        ServerStatusRequest request,
        CancellationToken cancellationToken)
    {
        return _serverStatusService.GetStatusAsync(request.Detail, cancellationToken);
    }
}
