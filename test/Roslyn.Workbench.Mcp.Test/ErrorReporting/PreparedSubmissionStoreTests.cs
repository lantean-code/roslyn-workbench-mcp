using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting.Preparation;

public sealed class PreparedSubmissionStoreTests
{
    private readonly Mock<IBoundedExpiringStore<string, PreparedSubmission>> _entries;
    private readonly PreparedSubmissionStore _target;
    private PreparedSubmission? _updatedSubmission;

    public PreparedSubmissionStoreTests()
    {
        _entries = new Mock<IBoundedExpiringStore<string, PreparedSubmission>>();
        _target = new PreparedSubmissionStore(_entries.Object);
    }

    [Fact]
    public void GIVEN_PreparedSubmission_WHEN_Adding_THEN_ShouldReturnStoreResult()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Prepared);
        _entries
            .Setup(item => item.TryAdd(submission.Handle, submission))
            .Returns(true);

        var wasAdded = _target.TryAdd(submission);

        wasAdded.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_Handle_WHEN_Reading_THEN_ShouldReturnStoreResult()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Prepared);
        PreparedSubmission? storedSubmission = submission;
        _entries
            .Setup(item => item.TryGet(submission.Handle, out storedSubmission))
            .Returns(true);

        var wasFound = _target.TryGet(submission.Handle, out var result);

        wasFound.Should().BeTrue();
        result.Should().BeSameAs(submission);
    }

    [Fact]
    public void GIVEN_UnknownHandle_WHEN_Acquiring_THEN_ShouldReturnUnknownOrExpired()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Prepared);
        SetupUpdateNotFound(submission.Handle);

        var result = _target.TryBeginSubmission(submission.Handle);

        result.Outcome.Should().Be(SubmissionAcquisitionOutcome.UnknownOrExpired);
        result.Submission.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PreparedSubmission_WHEN_Acquiring_THEN_ShouldTransitionToSending()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Prepared);
        SetupUpdate(submission);

        var result = _target.TryBeginSubmission(submission.Handle);

        result.Outcome.Should().Be(SubmissionAcquisitionOutcome.Acquired);
        result.Submission!.State.Should().Be(PreparedSubmissionState.Sending);
    }

    [Fact]
    public void GIVEN_SendingSubmission_WHEN_Acquiring_THEN_ShouldReturnInProgress()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Sending);
        SetupUpdate(submission);

        var result = _target.TryBeginSubmission(submission.Handle);

        result.Outcome.Should().Be(SubmissionAcquisitionOutcome.InProgress);
        result.Submission.Should().BeSameAs(submission);
    }

    [Fact]
    public void GIVEN_SentSubmission_WHEN_Acquiring_THEN_ShouldReturnOriginalReceipt()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Sent) with
        {
            Receipt = CreateReceipt(),
        };
        SetupUpdate(submission);

        var result = _target.TryBeginSubmission(submission.Handle);

        result.Outcome.Should().Be(SubmissionAcquisitionOutcome.AlreadySent);
        result.Submission.Should().BeSameAs(submission);
    }

    [Fact]
    public void GIVEN_SendingSubmission_WHEN_Completing_THEN_ShouldStoreSentReceipt()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Sending);
        var receipt = CreateReceipt();
        SetupUpdate(submission);

        _target.Complete(submission.Handle, receipt);

        VerifyUpdatedSubmission(
            submission.Handle,
            PreparedSubmissionState.Sent,
            receipt);
    }

    [Fact]
    public void GIVEN_PreparedSubmission_WHEN_Completing_THEN_ShouldLeaveSubmissionUnchanged()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Prepared);
        SetupUpdate(submission);

        _target.Complete(submission.Handle, CreateReceipt());

        VerifyUpdatedSubmission(
            submission.Handle,
            PreparedSubmissionState.Prepared,
            expectedReceipt: null);
    }

    [Fact]
    public void GIVEN_SendingSubmission_WHEN_ReleasingForRetry_THEN_ShouldReturnToPrepared()
    {
        var submission = CreateSubmission(PreparedSubmissionState.Sending);
        SetupUpdate(submission);

        _target.ReleaseForRetry(submission.Handle);

        VerifyUpdatedSubmission(
            submission.Handle,
            PreparedSubmissionState.Prepared,
            expectedReceipt: null);
    }

    [Fact]
    public void GIVEN_SentSubmission_WHEN_ReleasingForRetry_THEN_ShouldLeaveSubmissionUnchanged()
    {
        var receipt = CreateReceipt();
        var submission = CreateSubmission(PreparedSubmissionState.Sent) with
        {
            Receipt = receipt,
        };
        SetupUpdate(submission);

        _target.ReleaseForRetry(submission.Handle);

        VerifyUpdatedSubmission(
            submission.Handle,
            PreparedSubmissionState.Sent,
            receipt);
    }

    [Fact]
    public void GIVEN_Handle_WHEN_Discarding_THEN_ShouldRemoveSubmission()
    {
        _entries.Setup(item => item.Remove("Handle")).Returns(true);

        _target.Discard("Handle");

        _entries.Verify(item => item.Remove("Handle"), Times.Once);
    }

    private void SetupUpdate(PreparedSubmission submission)
    {
        _entries
            .Setup(item => item.Update(
                submission.Handle,
                It.IsAny<Func<PreparedSubmission, PreparedSubmission>>()))
            .Returns((string _, Func<PreparedSubmission, PreparedSubmission> update) =>
            {
                _updatedSubmission = update(submission);
                return BoundedExpiringStoreUpdateResult.Updated(
                    submission,
                    _updatedSubmission);
            });
    }

    private void SetupUpdateNotFound(string handle)
    {
        _entries
            .Setup(item => item.Update(
                handle,
                It.IsAny<Func<PreparedSubmission, PreparedSubmission>>()))
            .Returns(BoundedExpiringStoreUpdateResult.NotFound<PreparedSubmission>());
    }

    private void VerifyUpdatedSubmission(
        string handle,
        PreparedSubmissionState expectedState,
        ErrorSubmissionReceipt? expectedReceipt)
    {
        if (_updatedSubmission is not { } updatedSubmission)
        {
            throw new InvalidOperationException("The bounded store did not execute the supplied update.");
        }

        updatedSubmission.State.Should().Be(expectedState);
        updatedSubmission.Receipt.Should().Be(expectedReceipt);
        _entries.Verify(
            item => item.Update(
                handle,
                It.IsAny<Func<PreparedSubmission, PreparedSubmission>>()),
            Times.Once);
    }

    private static PreparedSubmission CreateSubmission(PreparedSubmissionState state)
    {
        return new PreparedSubmission
        {
            Handle = "Handle",
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T00:30:00Z", CultureInfo.InvariantCulture),
            State = state,
            Payload = new PreparedDispatchPayload<string>
            {
                DispatcherName = "DispatcherName",
                Destination = "Destination",
                ReportId = "ReportId",
                Report = CreateExternalReport(),
                PreviewBytes = ImmutableArray.Create<byte>(1, 2, 3),
                PreviewJson = "PreviewJson",
                DispatchState = "DispatchState",
            },
        };
    }

    private static ExternalErrorReport CreateExternalReport()
    {
        return new ExternalErrorReport
        {
            ReportId = "ReportId",
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            Tool = "Tool",
            ExecutionFamily = "ExecutionFamily",
            PluginClassification = "PluginClassification",
            DurationMilliseconds = 10,
            ExceptionClassification = "ExceptionClassification",
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "OperatingSystem",
            ProcessorArchitecture = "ProcessorArchitecture",
        };
    }

    private static ErrorSubmissionReceipt CreateReceipt()
    {
        return new ErrorSubmissionReceipt
        {
            Dispatcher = "Dispatcher",
            ReportReference = "ReportReference",
            PayloadDigest = "PayloadDigest",
        };
    }
}
