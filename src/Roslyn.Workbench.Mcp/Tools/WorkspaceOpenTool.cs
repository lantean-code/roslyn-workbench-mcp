using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceOpenTool : ServerOwnedToolBase<WorkspaceOpenRequest, WorkspaceOpenData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    public WorkspaceOpenTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.WorkspaceOpenName,
            title: "Workspace Open",
            description: "Loads an additional writable workspace. Open only a fully trusted workspace: loading evaluates MSBuild project logic, and later diagnostic or Code Action operations can load and execute project analyzers with the Host's permissions. If instance status reports that the workspace is or may be in use elsewhere, use it only for necessary queries, expect results to become stale, and coordinate mutation ownership before starting a transaction.",
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
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceOpenData
        {
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
        });
    }
}
