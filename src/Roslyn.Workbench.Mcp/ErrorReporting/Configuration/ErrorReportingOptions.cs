namespace Roslyn.Workbench.Mcp.ErrorReporting.Configuration;

internal sealed class ErrorReportingOptions
{
    public ErrorReportingConsentMode ConsentMode { get; set; } = ErrorReportingConsentMode.Prompt;

    public int CapturedErrorCapacity { get; set; } = 100;

    public TimeSpan CapturedErrorLifetime { get; set; } = TimeSpan.FromHours(1);

    public int MaximumCapturedErrorBytes { get; set; } = 64 * 1024;

    public int PreparedSubmissionCapacity { get; set; } = 50;

    public TimeSpan PreparedSubmissionLifetime { get; set; } = TimeSpan.FromMinutes(30);

    public int MaximumPayloadBytes { get; set; } = 64 * 1024;

    public bool AreReportingToolsEnabled => ConsentMode != ErrorReportingConsentMode.Never;
}
