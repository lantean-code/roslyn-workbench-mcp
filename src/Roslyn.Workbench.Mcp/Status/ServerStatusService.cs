using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Status;

internal sealed class ServerStatusService : IServerStatusService
{
    private static readonly string? _serverVersion = typeof(ServerStatusService).Assembly.GetName().Version?.ToString();
    private static readonly string? _roslynVersion = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString();

    private readonly StartupOptions _startupOptions;
    private readonly StartupConfigurationSnapshot _startupConfiguration;
    private readonly PluginCatalogSnapshot _pluginCatalogSnapshot;
    private readonly CodeActionCatalogSnapshot _codeActionCatalogSnapshot;
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly ICodeActionComposition _codeActionComposition;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly int _toolCount;
    private ServerConfiguration? _configuration;

    public ServerStatusService(
        IOptions<StartupOptions> startupOptions,
        StartupConfigurationSnapshot startupConfiguration,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        CodeActionCatalogSnapshot codeActionCatalogSnapshot,
        IMsBuildRegistrationService msBuildRegistrationService,
        ICodeActionComposition codeActionComposition,
        ICommitRecoveryStore recoveryStore)
    {
        _startupOptions = startupOptions.Value;
        _startupConfiguration = startupConfiguration;
        _pluginCatalogSnapshot = pluginCatalogSnapshot;
        _codeActionCatalogSnapshot = codeActionCatalogSnapshot;
        _msBuildRegistrationService = msBuildRegistrationService;
        _codeActionComposition = codeActionComposition;
        _recoveryStore = recoveryStore;
        _toolCount = _pluginCatalogSnapshot.Tools.Count
            + _codeActionCatalogSnapshot.Tools.Count
            + ServerOwnedToolRegistration.ToolCount;
    }

    public async ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(StatusDetailLevel detail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            plugins = _pluginCatalogSnapshot.Plugins;
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
            ToolCount = _toolCount,
            Plugins = plugins,
            Recovery = recovery,
        };

        return ToolResult.Succeeded(status);
    }

    private ServerConfiguration GetConfiguration()
    {
        return _configuration ??= new ServerConfiguration
        {
            DefaultMaxResults = _startupOptions.DefaultMaxResults,
            CodeActionReferenceLifetime = _startupOptions.CodeActionReferenceLifetime,
            MaxTransactionRevisions = _startupOptions.MaxTransactionRevisions,
            MaxConcurrentQueries = _startupOptions.MaxConcurrentQueries,
            ToolOutputSchemaMode = _startupOptions.ToolOutputSchemaMode,
        };
    }
}
