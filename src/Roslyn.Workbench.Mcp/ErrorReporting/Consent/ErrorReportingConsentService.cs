using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

/// <summary>
/// Maps the configured consent mode to the approval state used by error-reporting workflows.
/// </summary>
internal sealed class ErrorReportingConsentService : IErrorReportingConsentService
{
    private readonly ErrorReportingConsentMode _consentMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorReportingConsentService"/> class.
    /// </summary>
    /// <param name="options">The configured error-reporting consent mode.</param>
    public ErrorReportingConsentService(IOptions<ErrorReportingOptions> options)
    {
        _consentMode = options.Value.ConsentMode;
    }

    /// <summary>
    /// Gets the effective consent state for report preparation and submission.
    /// </summary>
    /// <returns>The consent state derived from server configuration.</returns>
    public ErrorReportingConsentState GetState()
    {
        switch (_consentMode)
        {
            case ErrorReportingConsentMode.Never:
                return ErrorReportingConsentState.Disabled;

            case ErrorReportingConsentMode.Prompt:
                return ErrorReportingConsentState.PromptRequired;

            case ErrorReportingConsentMode.Always:
                return ErrorReportingConsentState.AlwaysApproved;

            default:
                return ErrorReportingConsentState.Disabled;
        }
    }
}
