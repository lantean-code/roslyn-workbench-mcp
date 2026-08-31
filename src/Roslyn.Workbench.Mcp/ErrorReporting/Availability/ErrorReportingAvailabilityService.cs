using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

/// <summary>
/// Evaluates error reporting configuration, consent state and client elicitation support.
/// </summary>
internal sealed class ErrorReportingAvailabilityService : IErrorReportingAvailabilityService
{
    private readonly ErrorReportingOptions _options;
    private readonly IErrorReportingConsentService _consentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorReportingAvailabilityService"/> class.
    /// </summary>
    /// <param name="options">The server-wide error reporting configuration.</param>
    /// <param name="consentService">The service that resolves the effective consent mode.</param>
    public ErrorReportingAvailabilityService(
        IOptions<ErrorReportingOptions> options,
        IErrorReportingConsentService consentService)
    {
        _options = options.Value;
        _consentService = consentService;
    }

    /// <summary>
    /// Determines whether an error report can be prepared in the current request context.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace associated with the error, when available.</param>
    /// <param name="workspaceEpoch">The epoch of the workspace associated with the error, when available.</param>
    /// <param name="supportsElicitation">Whether the connected client supports MCP elicitation.</param>
    /// <returns>The effective reporting state and the tool available to continue the workflow.</returns>
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
