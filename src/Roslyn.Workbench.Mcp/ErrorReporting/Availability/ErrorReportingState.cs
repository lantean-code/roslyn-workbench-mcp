namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

/// <summary>
/// Defines the supported error reporting state values.
/// </summary>
internal enum ErrorReportingState
{
    /// <summary>
    /// Error reporting is available and requires per-report approval.
    /// </summary>
    Available,
    /// <summary>
    /// Error reporting is available and server configuration grants approval.
    /// </summary>
    AlwaysApproved,
    /// <summary>
    /// Server configuration disables error reporting.
    /// </summary>
    DisabledByConfiguration,
    /// <summary>
    /// Error reporting is configured but the client cannot obtain approval.
    /// </summary>
    ApprovalUnavailable,
}
