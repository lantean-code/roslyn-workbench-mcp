namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

internal sealed record ErrorReportingAvailability
{
    public required ErrorReportingState State { get; init; }

    public bool CanPrepare { get; init; }

    public string? PrepareTool { get; init; }
}
