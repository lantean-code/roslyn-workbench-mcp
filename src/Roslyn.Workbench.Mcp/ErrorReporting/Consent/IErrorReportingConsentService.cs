namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal interface IErrorReportingConsentService
{
    ErrorReportingConsentState GetState();
}
