using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceCloseTool : ServerOwnedToolBase<WorkspaceCloseRequest, WorkspaceCloseData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceCloseTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.WorkspaceCloseName,
            title: "Workspace Close",
            description: "Closes the selected workspace.",
            readOnly: false,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    protected override async ValueTask<ToolResult<WorkspaceCloseData>> ExecuteAsync(
        WorkspaceCloseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.CloseAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceCloseData
        {
            ClosedPath = data.ClosedPath,
        });
    }
}
