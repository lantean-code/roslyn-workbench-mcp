using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal sealed class ServerStatusService : IServerStatusService
{
    private readonly IOptions<StartupOptions> _startupOptions;
    private readonly PluginCatalogSnapshot _pluginCatalogSnapshot;
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly ICodeActionService _codeActionService;

    public ServerStatusService(
        IOptions<StartupOptions> startupOptions,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        IMsBuildRegistrationService msBuildRegistrationService,
        ICodeActionService codeActionService)
    {
        _startupOptions = startupOptions;
        _pluginCatalogSnapshot = pluginCatalogSnapshot;
        _msBuildRegistrationService = msBuildRegistrationService;
        _codeActionService = codeActionService;
    }

    public ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var serverAssembly = typeof(ServerStatusService).Assembly.GetName();
        var roslynAssembly = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName();
        var startupOptions = _startupOptions.Value;
        var includeExpandedDetail = detail == StatusDetailLevel.Full;

        return ValueTask.FromResult(ToolResult<ServerStatusData>.Succeeded(new ServerStatusData
        {
            ServerVersion = serverAssembly.Version?.ToString() ?? "0.0.0.0",
            RoslynVersion = roslynAssembly.Version?.ToString() ?? "0.0.0.0",
            MsBuild = _msBuildRegistrationService.CurrentStatus,
            CodeActions = _codeActionService.Status,
            Configuration = includeExpandedDetail
                ? new ServerConfiguration
                {
                    DefaultMaxResults = startupOptions.DefaultMaxResults,
                    MaxResponseBytes = startupOptions.MaxResponseBytes,
                    CodeActionTokenLifetime = startupOptions.CodeActionTokenLifetime,
                    MaxTransactionRevisions = startupOptions.MaxTransactionRevisions,
                    MaxConcurrentQueries = startupOptions.MaxConcurrentQueries,
                    ToolOutputSchemaMode = startupOptions.ToolOutputSchemaMode,
                }
                : null,
            ToolCount = _pluginCatalogSnapshot.Tools.Count + ServerOwnedToolRegistration.ToolCount,
            Plugins = includeExpandedDetail ? _pluginCatalogSnapshot.Plugins : null,
            Recovery = includeExpandedDetail ? CommitRecoveryStore.GetStatuses(startupOptions.StateDirectory) : null,
        }));
    }
}
