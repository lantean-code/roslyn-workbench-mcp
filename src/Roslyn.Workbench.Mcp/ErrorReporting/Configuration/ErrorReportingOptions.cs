namespace Roslyn.Workbench.Mcp.ErrorReporting.Configuration;

/// <summary>
/// Configures error-report consent, local retention and outbound payload limits.
/// </summary>
internal sealed class ErrorReportingOptions
{
    /// <summary>
    /// Gets or sets how user consent is obtained before a report is submitted.
    /// </summary>
    public ErrorReportingConsentMode ConsentMode { get; set; } = ErrorReportingConsentMode.Prompt;

    /// <summary>
    /// Gets or sets the maximum number of captured errors retained locally.
    /// </summary>
    public int CapturedErrorCapacity { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long a captured error remains available for inspection or preparation.
    /// </summary>
    public TimeSpan CapturedErrorLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum serialized size of one locally captured error.
    /// </summary>
    public int MaximumCapturedErrorBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of prepared submissions awaiting approval or dispatch.
    /// </summary>
    public int PreparedSubmissionCapacity { get; set; } = 50;

    /// <summary>
    /// Gets or sets how long a prepared submission remains valid.
    /// </summary>
    public TimeSpan PreparedSubmissionLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the maximum serialized size of a payload sent to a reporting provider.
    /// </summary>
    public int MaximumPayloadBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets a value indicating whether the error-reporting tools should be published.
    /// </summary>
    public bool AreReportingToolsEnabled => ConsentMode != ErrorReportingConsentMode.Never;
}
