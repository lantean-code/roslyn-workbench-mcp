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

        var consentState = _consentService.GetState();

        if (consentState == ErrorReportingConsentState.Disabled)
        {
            return Create(ErrorReportingState.DisabledByConfiguration, canPrepare: false);
        }

        if (consentState == ErrorReportingConsentState.PromptRequired
            && supportsElicitation == false)
        {
            return Create(ErrorReportingState.ApprovalUnavailable, canPrepare: false);
        }

        var state = consentState == ErrorReportingConsentState.AlwaysApproved
            ? ErrorReportingState.AlwaysApproved
            : ErrorReportingState.Available;

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
