using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Recreates a loaded workspace from its current on-disk inputs.
/// </summary>
internal sealed class WorkspaceReloadTool : ServerOwnedToolBase<WorkspaceReloadRequest, WorkspaceReloadData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceReloadTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="workspaceLifecycleService">The service that controls workspace loading and lifetime.</param>
    public WorkspaceReloadTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.WorkspaceReloadName,
            title: "Workspace Reload",
            description: "Reloads the selected workspace when it is out of date.",
            readOnly: false,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<WorkspaceReloadData>> ExecuteAsync(
        WorkspaceReloadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.ReloadAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, static data => new WorkspaceReloadData
        {
            Workspace = data.Workspace,
            ProjectCount = data.ProjectCount,
            DocumentCount = data.DocumentCount,
            LoadDiagnostics = data.LoadDiagnostics,
        });
    }
}
