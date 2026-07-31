namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal enum ErrorReportingConsentState
{
    PromptRequired,
    AlwaysApproved,
    AllowedForWorkspace,
    AllowedForSession,
    SuppressedForSession,
}
