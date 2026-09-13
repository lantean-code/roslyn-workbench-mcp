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
    private readonly OperationalPolicy _operationalPolicy;
    private readonly StartupConfigurationSnapshot _startupConfiguration;
    private readonly IPluginCatalogState _pluginCatalogState;
    private readonly CodeActionCatalogSnapshot _codeActionCatalogSnapshot;
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly ICodeActionComposition _codeActionComposition;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IErrorReportingConsentService _errorReportingConsentService;
    private readonly IErrorReportDispatcher _errorReportDispatcher;
    private readonly IWorkspaceAuthority _workspaceAuthority;
    private readonly ICommitConfirmationState _commitConfirmationState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStatusService"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="operationalPolicy">The effective immutable operational policy.</param>
    /// <param name="startupConfiguration">The resolved startup configuration reported by the service.</param>
    /// <param name="pluginCatalogState">The published plugin catalogue used to report or capture runtime state.</param>
    /// <param name="codeActionCatalogSnapshot">The immutable Code Action catalogue reported by server status.</param>
    /// <param name="msBuildRegistrationService">The MSBuild registration service.</param>
    /// <param name="codeActionComposition">The Roslyn composition state reported by server status.</param>
    /// <param name="recoveryStore">The store containing durable commit-recovery status.</param>
    /// <param name="errorReportingConsentService">The service that provides the effective reporting consent state.</param>
    /// <param name="errorReportDispatcher">The configured error-reporting provider.</param>
    /// <param name="workspaceAuthority">The effective Host-owned Workspace authority.</param>
    /// <param name="commitConfirmationState">The process-local transaction commit-confirmation state.</param>
    public ServerStatusService(
        IOptions<StartupOptions> startupOptions,
        OperationalPolicy operationalPolicy,
        StartupConfigurationSnapshot startupConfiguration,
        IPluginCatalogState pluginCatalogState,
        CodeActionCatalogSnapshot codeActionCatalogSnapshot,
        IMsBuildRegistrationService msBuildRegistrationService,
        ICodeActionComposition codeActionComposition,
        ICommitRecoveryStore recoveryStore,
        IErrorReportingConsentService errorReportingConsentService,
        IErrorReportDispatcher errorReportDispatcher,
        IWorkspaceAuthority workspaceAuthority,
        ICommitConfirmationState commitConfirmationState)
    {
        _startupOptions = startupOptions.Value;
        _operationalPolicy = operationalPolicy;
        _startupConfiguration = startupConfiguration;
        _pluginCatalogState = pluginCatalogState;
        _codeActionCatalogSnapshot = codeActionCatalogSnapshot;
        _msBuildRegistrationService = msBuildRegistrationService;
        _codeActionComposition = codeActionComposition;
        _recoveryStore = recoveryStore;
        _errorReportingConsentService = errorReportingConsentService;
        _errorReportDispatcher = errorReportDispatcher;
        _workspaceAuthority = workspaceAuthority;
        _commitConfirmationState = commitConfirmationState;
    }

    /// <summary>
    /// Gets the current status at the requested level of detail.
    /// </summary>
    /// <param name="detail">The requested level of detail for the server status response.</param>
    /// <param name="clientSupportsElicitation">Whether the connected client advertised MCP elicitation, when known.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing the server status response.</returns>
    public async ValueTask<ToolResult<ServerStatusData>> GetStatusAsync(
        StatusDetailLevel detail,
        bool? clientSupportsElicitation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pluginCatalog = _pluginCatalogState.Current.Catalog;
        var toolCount = pluginCatalog.Tools.Count
            + _codeActionCatalogSnapshot.Tools.Count
            + ServerOwnedToolRegistration.GetPublishedToolCount(_startupOptions.ErrorReporting, _operationalPolicy);

        var includeExpandedDetail = detail == StatusDetailLevel.Full;
        IReadOnlyList<RecoveryStatus>? recovery = null;
        ServerConfiguration? configuration = null;
        IReadOnlyList<WarningInfo>? startupWarnings = null;
        IReadOnlyList<PluginStatus>? plugins = null;

        if (includeExpandedDetail)
        {
            recovery = (await _recoveryStore.GetStatusesAsync(cancellationToken))
                .Select(ProjectRecoveryStatus)
                .ToArray();

            configuration = GetConfiguration(clientSupportsElicitation);
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

    private ServerConfiguration GetConfiguration(bool? clientSupportsElicitation)
    {
        return new ServerConfiguration
        {
            OperationalMode = GetOperationalModeName(_operationalPolicy.Mode),
            SourceMutationEnabled = _operationalPolicy.SourceMutationEnabled,
            ExternalPluginsEnabled = _startupOptions.ExternalPluginsEnabled,
            CommitConfirmationRequired = _operationalPolicy.CommitAuthorisation == CommitAuthorisationPolicy.Confirmation,
            ReceiptApprovalRequired = _operationalPolicy.CommitAuthorisation == CommitAuthorisationPolicy.ReceiptApproval,
            CompilerValidationRequired = false,
            ClientSupportsElicitation = clientSupportsElicitation,
            CommitConfirmationState = GetCommitConfirmationState(),
            WorkspaceAdmission = _workspaceAuthority.IsRestricted ? "Restricted" : "Unrestricted",
            AllowedWorkspaceRootCount = _workspaceAuthority.AllowedRootCount,
            ExternalDocumentPolicy = _workspaceAuthority.ExternalDocumentPolicy switch
            {
                ExternalDocumentPolicy.AllowReadOnly => "allow-read-only",
                ExternalDocumentPolicy.RejectWorkspace => "reject-workspace",
                _ => throw new InvalidOperationException("The external-document policy is not supported."),
            },
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

    private string GetCommitConfirmationState()
    {
        if (_operationalPolicy.CommitAuthorisation != CommitAuthorisationPolicy.Confirmation)
        {
            return "not-required";
        }

        return _commitConfirmationState.IsApprovedForSession
            ? "approved-for-session"
            : "required";
    }

    private static string GetOperationalModeName(OperationalMode mode)
    {
        return mode switch
        {
            OperationalMode.InspectionOnly => "inspection-only",
            OperationalMode.Transactional => "transactional",
            OperationalMode.ApprovalRequired => "approval-required",
            OperationalMode.AutonomousTrusted => "autonomous-trusted",
            _ => throw new InvalidOperationException("The operational mode is not supported."),
        };
    }

    private RecoveryStatus ProjectRecoveryStatus(RecoveryStatus status)
    {
        if (!_workspaceAuthority.IsRestricted)
        {
            return status;
        }

        if (CanExposeRecoveryPath(status))
        {
            return status;
        }

        return status with
        {
            Code = "RecoveryOutsideWorkspaceAuthority",
            SolutionPath = string.Empty,
            Message = "Restart the server with authority covering this Workspace to continue recovery.",
        };
    }

    private bool CanExposeRecoveryPath(RecoveryStatus status)
    {
        if (status.HasMalformedWorkspaceIdentity)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(status.SolutionPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(status.WorkspaceRoot))
        {
            return false;
        }

        return _workspaceAuthority.IsWorkspaceAllowed(status.SolutionPath, status.WorkspaceRoot);
    }
}
