using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Publishes Host configuration, component health, plugin status, and recovery diagnostics.
/// </summary>
internal sealed class ServerStatusTool : ServerOwnedToolBase<ServerStatusRequest, ServerStatusData>
{
    private readonly IServerStatusService _serverStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStatusTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="serverStatusService">The service that provides server status operations.</param>
    public ServerStatusTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IServerStatusService serverStatusService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.ServerStatusName,
            title: "Server Status",
            description: "Reports Host health without requiring a loaded workspace. Use this to diagnose startup, component availability, tool publication or effective operational policy; use workspace-status for one workspace and get-diagnostics for source findings. detail defaults to Minimal, returning server/Roslyn versions, MSBuild and Code Action component status and toolCount. Full additionally returns effective non-sensitive configuration, startup warnings, plugin status and unfinished recovery records; Standard currently has the same projection as Minimal. This read-only query does not load a workspace, run source diagnostics or repair recovery state.",
            readOnly: true,
            destructive: false,
            resultSummary: "server diagnostics, effective configuration, plugin status, and unfinished recovery state.")
    {
        _serverStatusService = serverStatusService;
    }

    /// <inheritdoc/>
    protected override ValueTask<ToolResult<ServerStatusData>> ExecuteAsync(
        ServerStatusRequest request,
        CancellationToken cancellationToken)
    {
        return _serverStatusService.GetStatusAsync(
            request.Detail,
            clientSupportsElicitation: null,
            cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask<ToolResult<ServerStatusData>> ExecuteAsync(
        ServerStatusRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var clientSupportsElicitation = requestContext.Server.ClientCapabilities?.Elicitation is not null;
        return _serverStatusService.GetStatusAsync(
            request.Detail,
            clientSupportsElicitation,
            cancellationToken);
    }
}
