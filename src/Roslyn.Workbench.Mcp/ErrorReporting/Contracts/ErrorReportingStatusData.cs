namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record ErrorReportingStatusData
{
    public required string Provider { get; init; }

    public required string ConsentMode { get; init; }

    public required string ConsentState { get; init; }
}
