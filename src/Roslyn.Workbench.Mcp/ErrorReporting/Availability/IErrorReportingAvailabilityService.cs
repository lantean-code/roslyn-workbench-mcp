namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

/// <summary>
/// Determines whether the configured consent workflow can prepare error reports for the current client.
/// </summary>
internal interface IErrorReportingAvailabilityService
{
    /// <summary>
    /// Determines whether an error report can be prepared in the current request context.
    /// </summary>
    /// <param name="workspaceId">The identifier of the workspace associated with the error, when available.</param>
    /// <param name="workspaceEpoch">The epoch of the workspace associated with the error, when available.</param>
    /// <param name="supportsElicitation">Whether the connected client supports MCP elicitation.</param>
    /// <returns>The effective reporting state and the tool available to continue the workflow.</returns>
    ErrorReportingAvailability GetAvailability(
        Guid? workspaceId,
        long? workspaceEpoch,
        bool? supportsElicitation);
}
