using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal sealed class WorkspaceStatusTool : ServerOwnedToolBase<WorkspaceStatusRequest, WorkspaceStatusData>
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;
    private readonly IErrorReportingConsentService _errorReportingConsentService;

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
