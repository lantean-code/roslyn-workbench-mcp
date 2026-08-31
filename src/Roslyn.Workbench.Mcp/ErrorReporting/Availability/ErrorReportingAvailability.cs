namespace Roslyn.Workbench.Mcp.ErrorReporting.Availability;

/// <summary>
/// Describes whether the current server and client can prepare an error report for approval.
/// </summary>
internal sealed record ErrorReportingAvailability
{
    /// <summary>
    /// Gets the reason error reporting is available or unavailable.
    /// </summary>
    public required ErrorReportingState State { get; init; }

    /// <summary>
    /// Gets a value indicating whether the prepare-error-report workflow may be started.
    /// </summary>
    public bool CanPrepare { get; init; }

    /// <summary>
    /// Gets the tool an agent can call to prepare a report, or <see langword="null"/> when preparation is unavailable.
    /// </summary>
    public string? PrepareTool { get; init; }
}
