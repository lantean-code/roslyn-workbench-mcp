namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Retains an immutable reviewed payload together with its lifetime and single-dispatch state.
/// </summary>
internal sealed record PreparedSubmission
{
    /// <summary>
    /// Gets the opaque handle used to retrieve and submit this payload.
    /// </summary>
    public required string Handle { get; init; }

    /// <summary>
    /// Gets the captured failure from which the payload was prepared.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Gets the time at which the payload was prepared.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the time after which the handle can no longer be used.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the provider-specific payload presented for review.
    /// </summary>
    public required PreparedDispatchPayload Payload { get; init; }

    /// <summary>
    /// Gets the workspace associated with the captured failure, when available.
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the workspace epoch associated with the captured failure, when available.
    /// </summary>
    public long? WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the current single-dispatch state of the submission.
    /// </summary>
    public PreparedSubmissionState State { get; init; }

    /// <summary>
    /// Gets the provider receipt after the submission has been sent.
    /// </summary>
    public ErrorSubmissionReceipt? Receipt { get; init; }
}
