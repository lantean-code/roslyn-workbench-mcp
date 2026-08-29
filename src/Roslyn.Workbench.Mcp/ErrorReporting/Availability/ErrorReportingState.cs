namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

internal enum ErrorReportingState
{
    Available,
    AlwaysApproved,
    DisabledByConfiguration,
    ApprovalUnavailable,
}
