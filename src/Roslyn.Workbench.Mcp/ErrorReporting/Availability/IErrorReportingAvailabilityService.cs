namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

internal interface IErrorReportingAvailabilityService
{
    ErrorReportingAvailability GetAvailability(
        Guid? workspaceId,
        long? workspaceEpoch,
        bool? supportsElicitation);
}
