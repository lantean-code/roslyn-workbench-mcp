using System.Diagnostics.CodeAnalysis;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Coordinates atomic acquisition, completion and retry of retained error-report submissions.
/// </summary>
internal sealed class PreparedSubmissionStore : IPreparedSubmissionStore
{
    private readonly IBoundedExpiringStore<string, PreparedSubmission> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedSubmissionStore"/> class.
    /// </summary>
    /// <param name="entries">The bounded store used to retain prepared submissions.</param>
    public PreparedSubmissionStore(IBoundedExpiringStore<string, PreparedSubmission> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// Attempts to retain a newly prepared submission under its opaque handle.
    /// </summary>
    /// <param name="submission">The reviewable submission to retain.</param>
    /// <returns><see langword="true"/> when the submission was retained; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(PreparedSubmission submission)
    {
        return _entries.TryAdd(submission.Handle, submission);
    }

    /// <summary>
    /// Attempts to retrieve an unexpired submission without changing its state.
    /// </summary>
    /// <param name="handle">The opaque handle returned when the submission was prepared.</param>
    /// <param name="submission">The retained submission when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an unexpired submission was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        string handle,
        [NotNullWhen(true)] out PreparedSubmission? submission)
    {
        return _entries.TryGet(handle, out submission);
    }

    /// <summary>
    /// Atomically acquires a prepared submission for dispatch.
    /// </summary>
    /// <param name="handle">The opaque handle returned when the submission was prepared.</param>
    /// <returns>The acquired submission or the reason it could not be acquired.</returns>
    public SubmissionAcquisition TryBeginSubmission(string handle)
    {
        var update = _entries.Update(handle, BeginSubmission);
        if (!update.WasFound)
        {
            return new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.UnknownOrExpired,
            };
        }

        if (update.OriginalValue.State == PreparedSubmissionState.Sending)
        {
            return new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.InProgress,
                Submission = update.OriginalValue,
            };
        }

        if (update.OriginalValue.State == PreparedSubmissionState.Sent)
        {
            return new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.AlreadySent,
                Submission = update.OriginalValue,
            };
        }

        return new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = update.UpdatedValue,
        };
    }

    /// <summary>
    /// Confirms that the submission is still acquired immediately before provider dispatch.
    /// </summary>
    /// <param name="handle">The handle of the acquired submission.</param>
    /// <returns><see langword="true"/> when the submission remains in the sending state; otherwise, <see langword="false"/>.</returns>
    public bool TryConfirmSubmission(string handle)
    {
        return _entries.TryGet(handle, out var submission)
            && submission.State == PreparedSubmissionState.Sending;
    }

    /// <summary>
    /// Marks an acquired submission as sent and stores its provider receipt.
    /// </summary>
    /// <param name="handle">The handle of the acquired submission.</param>
    /// <param name="receipt">The receipt returned after successful provider dispatch.</param>
    public void Complete(string handle, ErrorSubmissionReceipt receipt)
    {
        _entries.Update(
            handle,
            submission => CompleteSubmission(submission, receipt));
    }

    /// <summary>
    /// Returns an acquired submission to the prepared state after a retryable dispatch failure.
    /// </summary>
    /// <param name="handle">The handle of the acquired submission.</param>
    public void ReleaseForRetry(string handle)
    {
        _entries.Update(handle, ReleaseSubmissionForRetry);
    }

    /// <summary>
    /// Discards a prepared error submission.
    /// </summary>
    /// <param name="handle">The handle of the submission to remove.</param>
    public void Discard(string handle)
    {
        _entries.Remove(handle);
    }

    private static PreparedSubmission BeginSubmission(PreparedSubmission submission)
    {
        return submission.State == PreparedSubmissionState.Prepared
            ? submission with { State = PreparedSubmissionState.Sending }
            : submission;
    }

    private static PreparedSubmission CompleteSubmission(
        PreparedSubmission submission,
        ErrorSubmissionReceipt receipt)
    {
        if (submission.State != PreparedSubmissionState.Sending)
        {
            return submission;
        }

        return submission with
        {
            State = PreparedSubmissionState.Sent,
            Receipt = receipt,
        };
    }

    private static PreparedSubmission ReleaseSubmissionForRetry(PreparedSubmission submission)
    {
        return submission.State == PreparedSubmissionState.Sending
            ? submission with { State = PreparedSubmissionState.Prepared }
            : submission;
    }
}
