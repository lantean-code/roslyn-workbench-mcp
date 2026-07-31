using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal sealed class PreparedSubmissionStore : IPreparedSubmissionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PreparedSubmission> _submissions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly int _capacity;

    public PreparedSubmissionStore(
        IOptions<ErrorReportingOptions> options,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _capacity = options.Value.PreparedSubmissionCapacity;
    }

    public bool TryAdd(PreparedSubmission submission)
    {
        lock (_gate)
        {
            RemoveExpiredLocked();
            if (_submissions.Count >= _capacity)
            {
                return false;
            }

            return _submissions.TryAdd(submission.Handle, submission);
        }
    }

    public bool TryGet(
        string handle,
        [NotNullWhen(true)] out PreparedSubmission? submission)
    {
        lock (_gate)
        {
            RemoveExpiredLocked();
            return _submissions.TryGetValue(handle, out submission);
        }
    }

    public SubmissionAcquisition TryBeginSubmission(string handle)
    {
        lock (_gate)
        {
            RemoveExpiredLocked();
            if (!_submissions.TryGetValue(handle, out var submission))
            {
                return new SubmissionAcquisition
                {
                    Outcome = SubmissionAcquisitionOutcome.UnknownOrExpired,
                };
            }

            if (submission.State == PreparedSubmissionState.Sending)
            {
                return new SubmissionAcquisition
                {
                    Outcome = SubmissionAcquisitionOutcome.InProgress,
                    Submission = submission,
                };
            }

            if (submission.State == PreparedSubmissionState.Sent)
            {
                return new SubmissionAcquisition
                {
                    Outcome = SubmissionAcquisitionOutcome.AlreadySent,
                    Submission = submission,
                };
            }

            var acquired = submission with { State = PreparedSubmissionState.Sending };
            _submissions[handle] = acquired;

            return new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = acquired,
            };
        }
    }

    public void Complete(string handle, ErrorSubmissionReceipt receipt)
    {
        lock (_gate)
        {
            if (_submissions.TryGetValue(handle, out var submission)
                && submission.State == PreparedSubmissionState.Sending)
            {
                _submissions[handle] = submission with
                {
                    State = PreparedSubmissionState.Sent,
                    Receipt = receipt,
                };
            }
        }
    }

    public void ReleaseForRetry(string handle)
    {
        lock (_gate)
        {
            if (_submissions.TryGetValue(handle, out var submission)
                && submission.State == PreparedSubmissionState.Sending)
            {
                _submissions[handle] = submission with
                {
                    State = PreparedSubmissionState.Prepared,
                };
            }
        }
    }

    public void Discard(string handle)
    {
        lock (_gate)
        {
            _submissions.Remove(handle);
        }
    }

    private void RemoveExpiredLocked()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _submissions.ToArray())
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _submissions.Remove(pair.Key);
            }
        }
    }
}
