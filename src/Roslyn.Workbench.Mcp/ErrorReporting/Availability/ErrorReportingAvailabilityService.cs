using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

internal sealed class ErrorReportingAvailabilityService : IErrorReportingAvailabilityService
{
    private readonly ErrorReportingOptions _options;
    private readonly IErrorReportingConsentService _consentService;

    public ErrorReportingAvailabilityService(
        IOptions<ErrorReportingOptions> options,
        IErrorReportingConsentService consentService)
    {
        _options = options.Value;
        _consentService = consentService;
    }

    public ErrorReportingAvailability GetAvailability(
        Guid? workspaceId,
        long? workspaceEpoch,
        bool? supportsElicitation)
    {
        if (_options.ConsentMode == ErrorReportingConsentMode.Never)
        {
            return Create(ErrorReportingState.DisabledByConfiguration, canPrepare: false);
        }

        var consentState = _consentService.GetState(workspaceId, workspaceEpoch);
        if (consentState == ErrorReportingConsentState.SuppressedForSession)
        {
            return Create(ErrorReportingState.SuppressedForSession, canPrepare: false);
        }

        if (consentState == ErrorReportingConsentState.PromptRequired
            && supportsElicitation == false)
        {
            return Create(ErrorReportingState.ApprovalUnavailable, canPrepare: false);
        }

        var state = consentState switch
        {
            ErrorReportingConsentState.AlwaysApproved => ErrorReportingState.AlwaysApproved,
            ErrorReportingConsentState.AllowedForWorkspace => ErrorReportingState.AllowedForWorkspace,
            ErrorReportingConsentState.AllowedForSession => ErrorReportingState.AllowedForSession,
            _ => ErrorReportingState.Available,
        };

        return Create(state, canPrepare: true);
    }

    private static ErrorReportingAvailability Create(
        ErrorReportingState state,
        bool canPrepare)
    {
        return new ErrorReportingAvailability
        {
            State = state,
            CanPrepare = canPrepare,
            PrepareTool = canPrepare ? ServerOwnedToolRegistration.PrepareErrorReportName : null,
        };
    }
}
