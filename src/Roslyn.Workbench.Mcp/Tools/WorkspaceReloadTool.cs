using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceReloadTool : ServerOwnedToolBase<WorkspaceReloadRequest, WorkspaceReloadData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceReloadTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            name: ServerOwnedToolRegistration.WorkspaceReloadName,
            title: "Workspace Reload",
            description: "Reloads the selected workspace when it is out of date.",
            readOnly: false,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    protected override async ValueTask<ToolResult<WorkspaceReloadData>> ExecuteAsync(
        WorkspaceReloadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.ReloadAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceReloadData
        {
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
        });
    }
}
