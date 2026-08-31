using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Loads a solution or project as an additional writable workspace.
/// </summary>
internal sealed class WorkspaceOpenTool : ServerOwnedToolBase<WorkspaceOpenRequest, WorkspaceOpenData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceOpenTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="workspaceLifecycleService">The service that controls workspace loading and lifetime.</param>
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
            description: "Loads an additional writable workspace. Open only a fully trusted workspace: loading evaluates MSBuild project logic, evaluated source inputs including external linked or package-provided documents become queryable, and later diagnostic or Code Action operations can load and execute project analyzers with the Host's permissions. Documents outside workspaceRoot remain read-only. If instance status reports that the workspace is or may be in use elsewhere, use it only for necessary queries, expect results to become stale, and coordinate mutation ownership before starting a transaction.",
            readOnly: false,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<WorkspaceOpenData>> ExecuteAsync(
        WorkspaceOpenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.OpenAsync(
            request.Path,
            request.Alias,
            request.WorkspaceRoot,
            request.MsBuildProperties,
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
