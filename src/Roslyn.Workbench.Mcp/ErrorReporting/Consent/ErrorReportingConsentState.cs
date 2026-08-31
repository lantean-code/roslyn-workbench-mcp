namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

/// <summary>
/// Describes the approval requirement currently applied to error reports.
/// </summary>
internal enum ErrorReportingConsentState
{
    /// <summary>
    /// Error reporting is disabled by server configuration.
    /// </summary>
    Disabled,
    /// <summary>
    /// Each report requires explicit user approval.
    /// </summary>
    PromptRequired,
    /// <summary>
    /// Server configuration pre-approves report submission.
    /// </summary>
    AlwaysApproved,
}
