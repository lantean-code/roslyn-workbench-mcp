namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record ErrorReportingStatusData
{
    /// <summary>
    /// Gets the Provider.
    /// </summary>
    [Description("Configured error-reporting provider.")]
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the Consent Mode.
    /// </summary>
    [Description("Configured consent policy for submitting error reports.")]
    public required string ConsentMode { get; init; }

    /// <summary>
    /// Gets the Consent State.
    /// </summary>
    [Description("Current availability of user consent for error submission.")]
    public required string ConsentState { get; init; }
}
