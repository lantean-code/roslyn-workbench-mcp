namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Returns the immutable outbound payload and single-use handle presented for user review.
/// </summary>
internal sealed record PreparedErrorReportData
{
    /// <summary>
    /// Opaque handle required to submit this reviewed payload.
    /// </summary>
    [Description("Opaque handle required to submit this reviewed payload.")]
    public required string SubmissionHandle { get; init; }

    /// <summary>
    /// Dispatcher that will send the report.
    /// </summary>
    [Description("Dispatcher that will send the report.")]
    public required string Dispatcher { get; init; }

    /// <summary>
    /// External destination to which the report will be sent.
    /// </summary>
    [Description("External destination to which the report will be sent.")]
    public required string Destination { get; init; }

    /// <summary>
    /// Digest that identifies the exact immutable payload presented for review.
    /// </summary>
    [Description("Digest that identifies the exact immutable payload presented for review.")]
    public required string PayloadDigest { get; init; }

    /// <summary>
    /// Time after which the submission handle can no longer be used.
    /// </summary>
    [Description("Time after which the submission handle can no longer be used.")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Immutable JSON payload presented for user review.
    /// </summary>
    [Description("Immutable JSON payload presented for user review.")]
    public required string PayloadJson { get; init; }

    /// <summary>
    /// Data categories excluded from the prepared payload.
    /// </summary>
    [Description("Data categories excluded from the prepared payload.")]
    public IReadOnlyList<string> ExcludedCategories { get; init; } = [];

    /// <summary>
    /// Warnings the user should consider before approving submission.
    /// </summary>
    [Description("Warnings the user should consider before approving submission.")]
    public IReadOnlyList<string> ReviewWarnings { get; init; } = [];
}
