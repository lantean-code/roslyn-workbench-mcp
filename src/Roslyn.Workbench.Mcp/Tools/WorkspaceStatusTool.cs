using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceStatusTool : ServerOwnedToolBase<WorkspaceStatusRequest, WorkspaceStatusData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceStatusTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.WorkspaceStatusName,
            title: "Workspace Status",
            description: "Reports the selected workspace lifecycle and cross-instance state. Treat a workspace that is or may be in use elsewhere as query-only, use it only when necessary, and expect results to become stale.",
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
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceStatusData
        {
            State = data.State,
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
            Transaction = data.Transaction,
            ReloadRequired = data.ReloadRequired,
            Instances = data.Instances,
        });
    }
}
