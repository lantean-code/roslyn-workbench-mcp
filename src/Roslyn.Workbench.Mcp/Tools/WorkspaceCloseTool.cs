using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Closes a loaded workspace and releases its Host resources.
/// </summary>
internal sealed class WorkspaceCloseTool : ServerOwnedToolBase<WorkspaceCloseRequest, WorkspaceCloseData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceCloseTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="workspaceLifecycleService">The service that controls workspace loading and lifetime.</param>
    public WorkspaceCloseTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.WorkspaceCloseName,
            title: "Workspace Close",
            description: "Closes the selected workspace.",
            readOnly: false,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    /// <inheritdoc/>
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
