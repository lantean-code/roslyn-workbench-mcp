namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Records the provider reference and exact payload digest for a completed error submission.
/// </summary>
internal sealed record ErrorSubmissionReceipt
{
    /// <summary>
    /// Gets the provider that accepted the report.
    /// </summary>
    public required string Dispatcher { get; init; }

    /// <summary>
    /// Gets the provider's reference for the submitted report.
    /// </summary>
    public required string ReportReference { get; init; }

    /// <summary>
    /// Gets the digest of the exact payload sent to the provider.
    /// </summary>
    public required string PayloadDigest { get; init; }
}
