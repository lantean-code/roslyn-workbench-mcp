namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record SubmittedErrorReportData
{
    /// <summary>
    /// Gets the Dispatcher.
    /// </summary>
    [Description("Dispatcher that sent the report.")]
    public required string Dispatcher { get; init; }

    /// <summary>
    /// Gets the Report Reference.
    /// </summary>
    [Description("Provider reference for the submitted report.")]
    public required string ReportReference { get; init; }

    /// <summary>
    /// Gets the Payload Digest.
    /// </summary>
    [Description("Digest of the payload that was submitted.")]
    public required string PayloadDigest { get; init; }
}
