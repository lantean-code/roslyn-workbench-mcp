using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceOpenTool : ServerOwnedToolBase<WorkspaceOpenRequest, WorkspaceOpenData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceOpenTool(
        IOptions<StartupOptions> startupOptions,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            name: "workspace-open",
            title: "Workspace Open",
            description: "Loads an additional writable workspace.",
            readOnly: false,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    protected override async ValueTask<ToolResult<WorkspaceOpenData>> ExecuteAsync(
        WorkspaceOpenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.OpenAsync(
            request.Path,
            request.Alias,
            request.WorkspaceRoot,
            cancellationToken).ConfigureAwait(false);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceOpenData
        {
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
        });
    }
}
