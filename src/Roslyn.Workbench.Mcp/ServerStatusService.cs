using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp;

internal sealed class ServerStatusService : IServerStatusService
{
    private static readonly string _serverVersion = typeof(ServerStatusService).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    private static readonly string _roslynVersion = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    private readonly StartupOptions _startupOptions;
    private readonly PluginCatalogSnapshot _pluginCatalogSnapshot;
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly CodeActionRuntime _codeActionRuntime;
    private readonly int _toolCount;
    private ServerConfiguration? _configuration;

    public ServerStatusService(
        IOptions<StartupOptions> startupOptions,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        IMsBuildRegistrationService msBuildRegistrationService,
        CodeActionRuntime codeActionRuntime)
    {
        _startupOptions = startupOptions.Value;
        _pluginCatalogSnapshot = pluginCatalogSnapshot;
        _msBuildRegistrationService = msBuildRegistrationService;
        _codeActionRuntime = codeActionRuntime;
        _toolCount = _pluginCatalogSnapshot.Tools.Count + ServerOwnedToolRegistration.ToolCount;
    }

    public ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var includeExpandedDetail = detail == StatusDetailLevel.Full;

        return ValueTask.FromResult(ToolResult<ServerStatusData>.Succeeded(new ServerStatusData
        {
            ServerVersion = _serverVersion,
            RoslynVersion = _roslynVersion,
            MsBuild = _msBuildRegistrationService.CurrentStatus,
            CodeActions = _codeActionRuntime.Status,
            Configuration = includeExpandedDetail ? GetConfiguration() : null,
            ToolCount = _toolCount,
            Plugins = includeExpandedDetail ? _pluginCatalogSnapshot.Plugins : null,
            Recovery = includeExpandedDetail ? CommitRecoveryStore.GetStatuses(_startupOptions.StateDirectory) : null,
        }));
    }

    private ServerConfiguration GetConfiguration()
    {
        return _configuration ??= new ServerConfiguration
        {
            DefaultMaxResults = _startupOptions.DefaultMaxResults,
            CodeActionTokenLifetime = _startupOptions.CodeActionTokenLifetime,
            MaxTransactionRevisions = _startupOptions.MaxTransactionRevisions,
            MaxConcurrentQueries = _startupOptions.MaxConcurrentQueries,
            ToolOutputSchemaMode = _startupOptions.ToolOutputSchemaMode,
        };
    }
}
