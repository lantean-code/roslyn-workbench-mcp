namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record PreparedErrorReportData
{
    /// <summary>
    /// Gets the Submission Handle.
    /// </summary>
    [Description("Opaque handle required to submit this reviewed payload.")]
    public required string SubmissionHandle { get; init; }

    /// <summary>
    /// Gets the Dispatcher.
    /// </summary>
    [Description("Dispatcher that will send the report.")]
    public required string Dispatcher { get; init; }

    /// <summary>
    /// Gets the Destination.
    /// </summary>
    [Description("External destination to which the report will be sent.")]
    public required string Destination { get; init; }

    /// <summary>
    /// Gets the Payload Digest.
    /// </summary>
    [Description("Digest that identifies the exact immutable payload presented for review.")]
    public required string PayloadDigest { get; init; }

    /// <summary>
    /// Gets the Expires At.
    /// </summary>
    [Description("Time after which the submission handle can no longer be used.")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the Payload Json.
    /// </summary>
    [Description("Immutable JSON payload presented for user review.")]
    public required string PayloadJson { get; init; }

    /// <summary>
    /// Gets the Excluded Categories.
    /// </summary>
    [Description("Data categories excluded from the prepared payload.")]
    public IReadOnlyList<string> ExcludedCategories { get; init; } = [];

    /// <summary>
    /// Gets the Review Warnings.
    /// </summary>
    [Description("Warnings the user should consider before approving submission.")]
    public IReadOnlyList<string> ReviewWarnings { get; init; } = [];
}
