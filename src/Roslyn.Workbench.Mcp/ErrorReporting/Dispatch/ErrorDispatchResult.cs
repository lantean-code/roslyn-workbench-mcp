using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Reports whether a provider accepted an error report and identifies either the submission or its failure.
/// </summary>
internal sealed record ErrorDispatchResult
{
    /// <summary>
    /// Gets whether the provider accepted or rejected the report.
    /// </summary>
    public ErrorDispatchOutcome Outcome { get; }

    /// <summary>
    /// Gets the provider's reference for an accepted report.
    /// </summary>
    public string? ReportReference { get; }

    /// <summary>
    /// Gets the digest of the exact payload sent to the provider.
    /// </summary>
    public string? PayloadDigest { get; }

    /// <summary>
    /// Gets the stable failure code when dispatch was rejected.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the diagnostic failure message when dispatch was rejected.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets whether the provider accepted the report.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ReportReference), nameof(PayloadDigest))]
    [MemberNotNullWhen(false, nameof(ErrorCode), nameof(ErrorMessage))]
    public bool IsAccepted => Outcome == ErrorDispatchOutcome.Accepted;

    private ErrorDispatchResult(
        ErrorDispatchOutcome outcome,
        string? reportReference,
        string? payloadDigest,
        string? errorCode,
        string? errorMessage)
    {
        Outcome = outcome;
        ReportReference = reportReference;
        PayloadDigest = payloadDigest;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a result for a report accepted by the provider.
    /// </summary>
    /// <param name="reportReference">The provider's reference for the accepted report.</param>
    /// <param name="payloadDigest">The digest of the exact payload sent to the provider.</param>
    /// <returns>An accepted dispatch result.</returns>
    public static ErrorDispatchResult Accepted(string reportReference, string payloadDigest)
    {
        return new ErrorDispatchResult(
            ErrorDispatchOutcome.Accepted,
            reportReference,
            payloadDigest,
            errorCode: null,
            errorMessage: null);
    }

    /// <summary>
    /// Creates a result for a report rejected by the provider.
    /// </summary>
    /// <param name="errorCode">The stable dispatch failure code.</param>
    /// <param name="errorMessage">The diagnostic dispatch failure message.</param>
    /// <returns>A rejected dispatch result.</returns>
    public static ErrorDispatchResult Rejected(string errorCode, string errorMessage)
    {
        return new ErrorDispatchResult(
            ErrorDispatchOutcome.Rejected,
            reportReference: null,
            payloadDigest: null,
            errorCode,
            errorMessage);
    }
}
