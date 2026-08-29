using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal sealed class ErrorReportingConsentService : IErrorReportingConsentService
{
    private readonly ErrorReportingConsentMode _consentMode;

    public ErrorReportingConsentService(IOptions<ErrorReportingOptions> options)
    {
        _consentMode = options.Value.ConsentMode;
    }

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
