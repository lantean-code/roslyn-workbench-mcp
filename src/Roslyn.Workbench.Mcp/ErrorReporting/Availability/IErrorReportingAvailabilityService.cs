namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

internal interface IErrorReportingAvailabilityService
{
    ErrorReportingAvailability GetAvailability(
        string? workspaceId,
        long? workspaceEpoch,
        bool? supportsElicitation);
}
