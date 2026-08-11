using System.Diagnostics.CodeAnalysis;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal sealed class PreparedSubmissionStore : IPreparedSubmissionStore
{
    private readonly IBoundedExpiringStore<string, PreparedSubmission> _entries;

    public PreparedSubmissionStore(IBoundedExpiringStore<string, PreparedSubmission> entries)
    {
        _entries = entries;
    }

    public bool TryAdd(PreparedSubmission submission)
    {
        return _entries.TryAdd(submission.Handle, submission);
    }

    public bool TryGet(
        string handle,
        [NotNullWhen(true)] out PreparedSubmission? submission)
    {
        return _entries.TryGet(handle, out submission);
    }

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

    public void Complete(string handle, ErrorSubmissionReceipt receipt)
    {
        _entries.Update(
            handle,
            submission => CompleteSubmission(submission, receipt));
    }

    public void ReleaseForRetry(string handle)
    {
        _entries.Update(handle, ReleaseSubmissionForRetry);
    }

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
