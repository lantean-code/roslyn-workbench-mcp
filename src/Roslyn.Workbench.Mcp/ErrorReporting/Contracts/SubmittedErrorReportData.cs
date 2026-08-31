namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Confirms which reviewed payload was submitted and provides its provider reference.
/// </summary>
internal sealed record SubmittedErrorReportData
{
    /// <summary>
    /// Dispatcher that sent the report.
    /// </summary>
    [Description("Dispatcher that sent the report.")]
    public required string Dispatcher { get; init; }

    /// <summary>
    /// Provider reference for the submitted report.
    /// </summary>
    [Description("Provider reference for the submitted report.")]
    public required string ReportReference { get; init; }

    /// <summary>
    /// Digest of the payload that was submitted.
    /// </summary>
    [Description("Digest of the payload that was submitted.")]
    public required string PayloadDigest { get; init; }
}
