using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceListTool : ServerOwnedToolBase<WorkspaceListRequest, WorkspaceListData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceListTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.WorkspaceListName,
            title: "Workspace List",
            description: "Lists the currently loaded workspaces.",
            readOnly: true,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    protected override async ValueTask<ToolResult<WorkspaceListData>> ExecuteAsync(
        WorkspaceListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.ListAsync(cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceListData
        {
            Workspaces = data.Workspaces,
            TransactionOwnerWorkspaceId = data.TransactionOwnerWorkspaceId,
        });
    }
}
