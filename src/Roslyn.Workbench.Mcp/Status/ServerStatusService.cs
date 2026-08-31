using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Status;

/// <summary>
/// Aggregates host, plugin, Code Action, recovery and error-reporting state for the server-status tool.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStatusService"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="startupConfiguration">The resolved startup configuration reported by the service.</param>
    /// <param name="pluginCatalogState">The published plugin catalogue used to report or capture runtime state.</param>
    /// <param name="codeActionCatalogSnapshot">The immutable Code Action catalogue reported by server status.</param>
    /// <param name="msBuildRegistrationService">The MSBuild registration service.</param>
    /// <param name="codeActionComposition">The Roslyn composition state reported by server status.</param>
    /// <param name="recoveryStore">The store containing durable commit-recovery status.</param>
    /// <param name="errorReportingConsentService">The service that provides the effective reporting consent state.</param>
    /// <param name="errorReportDispatcher">The configured error-reporting provider.</param>
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

    /// <summary>
    /// Gets the current status at the requested level of detail.
    /// </summary>
    /// <param name="detail">The requested level of detail for the server status response.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing the server status response.</returns>
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
