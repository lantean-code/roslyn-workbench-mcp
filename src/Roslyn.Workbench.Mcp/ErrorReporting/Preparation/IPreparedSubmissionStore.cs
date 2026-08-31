using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Retains prepared submissions and coordinates their single-dispatch state transitions.
/// </summary>
internal interface IPreparedSubmissionStore
{
    /// <summary>
    /// Attempts to retain a newly prepared submission under its opaque handle.
    /// </summary>
    /// <param name="submission">The reviewable submission to retain.</param>
    /// <returns><see langword="true"/> when the submission was retained; otherwise, <see langword="false"/>.</returns>
    bool TryAdd(PreparedSubmission submission);

    /// <summary>
    /// Attempts to retrieve an unexpired submission without changing its state.
    /// </summary>
    /// <param name="handle">The opaque handle returned when the submission was prepared.</param>
    /// <param name="submission">The retained submission when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an unexpired submission was found; otherwise, <see langword="false"/>.</returns>
    bool TryGet(string handle, [NotNullWhen(true)] out PreparedSubmission? submission);

    /// <summary>
    /// Atomically acquires a prepared submission for dispatch.
    /// </summary>
    /// <param name="handle">The opaque handle returned when the submission was prepared.</param>
    /// <returns>The acquired submission or the reason it could not be acquired.</returns>
    SubmissionAcquisition TryBeginSubmission(string handle);

    /// <summary>
    /// Confirms that the submission is still acquired immediately before provider dispatch.
    /// </summary>
    /// <param name="handle">The handle of the acquired submission.</param>
    /// <returns><see langword="true"/> when the submission remains in the sending state; otherwise, <see langword="false"/>.</returns>
    bool TryConfirmSubmission(string handle);

    /// <summary>
    /// Marks an acquired submission as sent and stores its provider receipt.
    /// </summary>
    /// <param name="handle">The handle of the acquired submission.</param>
    /// <param name="receipt">The receipt returned after successful provider dispatch.</param>
    void Complete(string handle, ErrorSubmissionReceipt receipt);

    /// <summary>
    /// Returns an acquired submission to the prepared state after a retryable dispatch failure.
    /// </summary>
    /// <param name="handle">The handle of the acquired submission.</param>
    void ReleaseForRetry(string handle);

    /// <summary>
    /// Discards a prepared error submission.
    /// </summary>
    /// <param name="handle">The handle of the submission to remove.</param>
    void Discard(string handle);
}
