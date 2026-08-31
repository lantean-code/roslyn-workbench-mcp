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
            description: "Returns server diagnostics without requiring a loaded workspace.",
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
        return _serverStatusService.GetStatusAsync(request.Detail, cancellationToken);
    }
}
