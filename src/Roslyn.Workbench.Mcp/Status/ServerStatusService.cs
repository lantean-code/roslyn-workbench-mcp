using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Status;

internal sealed class ServerStatusService : IServerStatusService
{
    private static readonly string? _serverVersion = typeof(ServerStatusService).Assembly.GetName().Version?.ToString();
    private static readonly string? _roslynVersion = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString();

    private readonly StartupOptions _startupOptions;
    private readonly StartupConfigurationSnapshot _startupConfiguration;
    private readonly IPluginCatalogState _pluginCatalogState;
    private readonly CodeActionCatalogSnapshot _codeActionCatalogSnapshot;
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly ICodeActionComposition _codeActionComposition;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IErrorReportingConsentService _errorReportingConsentService;
    private readonly IErrorReportDispatcher _errorReportDispatcher;

    public ServerStatusService(
        IOptions<StartupOptions> startupOptions,
        StartupConfigurationSnapshot startupConfiguration,
        IPluginCatalogState pluginCatalogState,
        CodeActionCatalogSnapshot codeActionCatalogSnapshot,
        IMsBuildRegistrationService msBuildRegistrationService,
        ICodeActionComposition codeActionComposition,
        ICommitRecoveryStore recoveryStore,
        IErrorReportingConsentService errorReportingConsentService,
        IErrorReportDispatcher errorReportDispatcher)
    {
        _startupOptions = startupOptions.Value;
        _startupConfiguration = startupConfiguration;
        _pluginCatalogState = pluginCatalogState;
        _codeActionCatalogSnapshot = codeActionCatalogSnapshot;
        _msBuildRegistrationService = msBuildRegistrationService;
        _codeActionComposition = codeActionComposition;
        _recoveryStore = recoveryStore;
        _errorReportingConsentService = errorReportingConsentService;
        _errorReportDispatcher = errorReportDispatcher;
    }

    public async ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pluginCatalog = _pluginCatalogState.Current.Catalog;
        var toolCount = pluginCatalog.Tools.Count
            + _codeActionCatalogSnapshot.Tools.Count
            + ServerOwnedToolRegistration.GetPublishedToolCount(_startupOptions.ErrorReporting);

        var includeExpandedDetail = detail == StatusDetailLevel.Full;
        IReadOnlyList<RecoveryStatus>? recovery = null;
        ServerConfiguration? configuration = null;
        IReadOnlyList<WarningInfo>? startupWarnings = null;
        IReadOnlyList<PluginStatus>? plugins = null;

        if (includeExpandedDetail)
        {
            recovery = await _recoveryStore.GetStatusesAsync(cancellationToken);
            configuration = GetConfiguration();
            startupWarnings = _startupConfiguration.Warnings;
            plugins = pluginCatalog.Plugins;
        }

        var codeActions = new ComponentStatus
        {
            IsAvailable = _codeActionComposition.Status.IsAvailable,
            Version = _codeActionComposition.Status.Version,
            Message = _codeActionComposition.Status.Message,
        };

        var status = new ServerStatusData
        {
            ServerVersion = _serverVersion,
            RoslynVersion = _roslynVersion,
            MsBuild = _msBuildRegistrationService.CurrentStatus,
            CodeActions = codeActions,
            Configuration = configuration,
            StartupWarnings = startupWarnings,
            ToolCount = toolCount,
            Plugins = plugins,
            Recovery = recovery,
        };

        return ToolResult.Succeeded(status);
    }

    private ServerConfiguration GetConfiguration()
    {
        return new ServerConfiguration
        {
            DefaultMaxResults = _startupOptions.DefaultMaxResults,
            CodeActionReferenceLifetime = _startupOptions.CodeActionReferenceLifetime,
            MaxTransactionRevisions = _startupOptions.MaxTransactionRevisions,
            MaxConcurrentQueries = _startupOptions.MaxConcurrentQueries,
            ToolOutputSchemaMode = _startupOptions.ToolOutputSchemaMode,
            ErrorReporting = new ErrorReportingStatusData
            {
                Provider = _errorReportDispatcher.Name,
                ConsentMode = _startupOptions.ErrorReporting.ConsentMode.ToString(),
                ConsentState = _errorReportingConsentService
                    .GetState()
                    .ToString(),
            },
        };
    }
}
