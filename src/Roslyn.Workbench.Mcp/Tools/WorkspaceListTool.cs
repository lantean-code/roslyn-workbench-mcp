using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Lists loaded workspaces and identifies the current transaction owner.
/// </summary>
internal sealed class WorkspaceListTool : ServerOwnedToolBase<WorkspaceListRequest, WorkspaceListData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceListTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="workspaceLifecycleService">The service that controls workspace loading and lifetime.</param>
    public WorkspaceListTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IWorkspaceLifecycleService workspaceLifecycleService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.WorkspaceListName,
            title: "Workspace List",
            description: "Lists workspaces currently loaded in this Host, returning their identities and transactionOwnerWorkspaceId when a transaction is active. Use this to discover workspaceId, alias or path selectors before workspace-status or other workspace tools; use workspace-open to load a new workspace. Takes no workspace selector and can return an empty list. This read-only inventory does not discover repositories on disk or workspaces loaded only by other Host processes. Use workspace-status for lifecycle, reload requirements and advisory cross-instance information.",
            readOnly: true,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
    }

    /// <inheritdoc/>
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
