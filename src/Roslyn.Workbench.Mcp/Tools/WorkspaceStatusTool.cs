using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceStatusTool : ServerOwnedToolBase<WorkspaceStatusRequest, WorkspaceStatusData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceStatusTool(
        IOptions<StartupOptions> startupOptions,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            name: "workspace-status",
            title: "Workspace Status",
            description: "Reports the selected workspace lifecycle state.",
            readOnly: true,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    protected override async ValueTask<ToolResult<WorkspaceStatusData>> ExecuteAsync(
        WorkspaceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.GetStatusAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.Detail,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceStatusData
        {
            State = data.State,
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
            Transaction = data.Transaction,
            ReloadRequired = data.ReloadRequired,
        });
    }
}
