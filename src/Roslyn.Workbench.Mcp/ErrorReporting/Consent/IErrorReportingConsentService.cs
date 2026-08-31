namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

/// <summary>
/// Provides the effective approval requirement for error-reporting workflows.
/// </summary>
internal interface IErrorReportingConsentService
{
    /// <summary>
    /// Gets the effective consent state for report preparation and submission.
    /// </summary>
    /// <returns>The consent state derived from server configuration.</returns>
    ErrorReportingConsentState GetState();
}
