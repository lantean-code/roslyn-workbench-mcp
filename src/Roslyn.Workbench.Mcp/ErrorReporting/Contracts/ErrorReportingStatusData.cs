namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Reports the configured error-reporting provider and effective consent state.
/// </summary>
internal sealed record ErrorReportingStatusData
{
    /// <summary>
    /// Configured error-reporting provider.
    /// </summary>
    [Description("Configured error-reporting provider.")]
    public required string Provider { get; init; }

    /// <summary>
    /// Configured consent policy for submitting error reports.
    /// </summary>
    [Description("Configured consent policy for submitting error reports.")]
    public required string ConsentMode { get; init; }

    /// <summary>
    /// Current availability of user consent for error submission.
    /// </summary>
    [Description("Current availability of user consent for error submission.")]
    public required string ConsentState { get; init; }
}
