using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceListTool : ServerOwnedToolBase<WorkspaceListRequest, WorkspaceListData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceListTool(
        IOptions<StartupOptions> startupOptions,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            name: "workspace-list",
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
        var result = await _workspaceLifecycleService.ListAsync(cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceListData
        {
            Workspaces = data.Workspaces,
            TransactionOwnerWorkspaceId = data.TransactionOwnerWorkspaceId,
        });
    }
}
