namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

internal enum ErrorReportingState
{
    Available,
    AlwaysApproved,
    AllowedForWorkspace,
    AllowedForSession,
    SuppressedForSession,
    DisabledByConfiguration,
    ApprovalUnavailable,
}
