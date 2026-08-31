using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Reports lifecycle, transaction, external-change, and cross-instance state for a workspace.
/// </summary>
internal sealed class WorkspaceStatusTool : ServerOwnedToolBase<WorkspaceStatusRequest, WorkspaceStatusData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;
    private readonly IErrorReportingConsentService _errorReportingConsentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceStatusTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="workspaceLifecycleService">The service that controls workspace loading and lifetime.</param>
    /// <param name="errorReportingConsentService">The service that provides error reporting consent operations.</param>
    public WorkspaceStatusTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IWorkspaceLifecycleService workspaceLifecycleService,
        IErrorReportingConsentService errorReportingConsentService)
        : base(
            startupOptions: startupOptions,
            protocolFactory: protocolFactory,
            requestBinder: requestBinder,
            name: ServerOwnedToolRegistration.WorkspaceStatusName,
            title: "Workspace Status",
            description: "Reports the selected workspace lifecycle and cross-instance state. Treat a workspace that is or may be in use elsewhere as query-only, use it only when necessary, and expect results to become stale.",
            readOnly: true,
            destructive: false)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
        _errorReportingConsentService = errorReportingConsentService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<WorkspaceStatusData>> ExecuteAsync(
        WorkspaceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceLifecycleService.GetStatusAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.Detail,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, CreateData);
    }

    private WorkspaceStatusData CreateData(WorkspaceStatusOutcome outcome)
    {
        WorkspaceExternalChangeData? externalChange = null;
        if (outcome.ExternalChange is not null)
        {
            externalChange = new WorkspaceExternalChangeData
            {
                DetectionSource = outcome.ExternalChange.DetectionSource.ToString(),
                ErrorCode = outcome.ExternalChange.ErrorCode?.ToString(),
                Kind = outcome.ExternalChange.Kind.ToString(),
                Path = outcome.ExternalChange.Path,
                PreviousPath = outcome.ExternalChange.PreviousPath,
            };
        }

        return new WorkspaceStatusData
        {
            State = outcome.State,
            Workspace = outcome.Workspace,
            ProjectCount = outcome.ProjectCount,
            DocumentCount = outcome.DocumentCount,
            LoadDiagnostics = outcome.LoadDiagnostics,
            Transaction = outcome.Transaction,
            ReloadRequired = outcome.ReloadRequired,
            ExternalChange = externalChange,
            Instances = outcome.Instances,
            ErrorReportingConsent = _errorReportingConsentService
                .GetState()
                .ToString(),
        };
    }
}
