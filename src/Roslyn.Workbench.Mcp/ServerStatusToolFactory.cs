using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal static class ServerStatusToolFactory
{
    public static McpServerTool Create(StartupOptions startupOptions, PluginCatalogSnapshot pluginCatalogSnapshot, ComponentStatus codeActions, int toolCount)
    {
        ArgumentNullException.ThrowIfNull(startupOptions);
        ArgumentNullException.ThrowIfNull(pluginCatalogSnapshot);
        ArgumentNullException.ThrowIfNull(codeActions);

        return new ServerToolMcpServerTool<ServerStatusRequest, ServerStatusData>(
            "server-status",
            "Server Status",
            "Returns server diagnostics without requiring a loaded workspace.",
            readOnly: true,
            destructive: false,
            startupOptions.ToolOutputSchemaMode,
            "server diagnostics, effective configuration, plugin status, and unfinished recovery state.",
            (_, requestContext, cancellationToken) => ValueTask.FromResult(CreateResult(startupOptions, pluginCatalogSnapshot, codeActions, toolCount, requestContext, cancellationToken)));
    }

    private static ToolResult<ServerStatusData> CreateResult(
        StartupOptions startupOptions,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        ComponentStatus codeActions,
        int toolCount,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var serverAssembly = typeof(ServerStatusToolFactory).Assembly.GetName();
        var roslynAssembly = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName();

        return ToolResult<ServerStatusData>.Succeeded(new ServerStatusData
        {
            ServerVersion = serverAssembly.Version?.ToString() ?? "0.0.0.0",
            ProtocolVersion = requestContext.Server.NegotiatedProtocolVersion ?? string.Empty,
            RoslynVersion = roslynAssembly.Version?.ToString() ?? "0.0.0.0",
            MsBuild = MsBuildRegistration.CurrentStatus,
            CodeActions = codeActions,
            Configuration = new ServerConfiguration
            {
                DefaultMaxResults = startupOptions.DefaultMaxResults,
                MaxResponseBytes = startupOptions.MaxResponseBytes,
                CodeActionTokenLifetime = startupOptions.CodeActionTokenLifetime,
                MaxTransactionRevisions = startupOptions.MaxTransactionRevisions,
                MaxConcurrentQueries = startupOptions.MaxConcurrentQueries,
                ToolOutputSchemaMode = startupOptions.ToolOutputSchemaMode,
            },
            ToolCount = toolCount,
            Plugins = pluginCatalogSnapshot.Plugins,
            Recovery = CommitRecoveryStore.GetStatuses(startupOptions.StateDirectory),
        });
    }
}
