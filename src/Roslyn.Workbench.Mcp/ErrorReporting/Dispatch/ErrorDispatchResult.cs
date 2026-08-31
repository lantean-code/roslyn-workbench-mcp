namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Reports whether a provider accepted an error report and identifies either the submission or its failure.
/// </summary>
internal sealed record ErrorDispatchResult
{
    /// <summary>
    /// Gets whether the provider accepted or rejected the report.
    /// </summary>
    public required ErrorDispatchOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the provider's reference for an accepted report.
    /// </summary>
    public string? ReportReference { get; init; }

    /// <summary>
    /// Gets the digest of the exact payload sent to the provider.
    /// </summary>
    public string? PayloadDigest { get; init; }

    /// <summary>
    /// Gets the stable failure code when dispatch was rejected.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the diagnostic failure message when dispatch was rejected.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
