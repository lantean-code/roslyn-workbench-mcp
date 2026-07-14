using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp;

internal sealed class ServerStatusService : IServerStatusService
{
    private static readonly string _serverVersion = typeof(ServerStatusService).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    private static readonly string _roslynVersion = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    private readonly StartupOptions _startupOptions;
    private readonly PluginCatalogSnapshot _pluginCatalogSnapshot;
    private readonly CodeActionCatalogSnapshot _codeActionCatalogSnapshot;
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly ICodeActionProviderCatalog _codeActionProviderCatalog;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly int _toolCount;
    private ServerConfiguration? _configuration;

    public ServerStatusService(
        IOptions<StartupOptions> startupOptions,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        CodeActionCatalogSnapshot codeActionCatalogSnapshot,
        IMsBuildRegistrationService msBuildRegistrationService,
        ICodeActionProviderCatalog codeActionProviderCatalog,
        ICommitRecoveryStore recoveryStore)
    {
        _startupOptions = startupOptions.Value;
        _pluginCatalogSnapshot = pluginCatalogSnapshot;
        _codeActionCatalogSnapshot = codeActionCatalogSnapshot;
        _msBuildRegistrationService = msBuildRegistrationService;
        _codeActionProviderCatalog = codeActionProviderCatalog;
        _recoveryStore = recoveryStore;
        _toolCount = _pluginCatalogSnapshot.Tools.Count
            + _codeActionCatalogSnapshot.Tools.Count
            + ServerOwnedToolRegistration.ToolCount;
    }

    public async ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var includeExpandedDetail = detail == StatusDetailLevel.Full;

        var recovery = includeExpandedDetail
            ? await _recoveryStore.GetStatusesAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return ToolResult<ServerStatusData>.Succeeded(new ServerStatusData
        {
            ServerVersion = _serverVersion,
            RoslynVersion = _roslynVersion,
            MsBuild = _msBuildRegistrationService.CurrentStatus,
            CodeActions = new ComponentStatus
            {
                IsAvailable = _codeActionProviderCatalog.Status.IsAvailable,
                Version = _codeActionProviderCatalog.Status.Version,
                Message = _codeActionProviderCatalog.Status.Message,
            },
            Configuration = includeExpandedDetail ? GetConfiguration() : null,
            ToolCount = _toolCount,
            Plugins = includeExpandedDetail ? _pluginCatalogSnapshot.Plugins : null,
            Recovery = recovery,
        });
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
