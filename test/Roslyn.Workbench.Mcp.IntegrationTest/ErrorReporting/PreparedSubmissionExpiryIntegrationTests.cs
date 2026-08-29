using System.Collections.Immutable;
using System.Globalization;
using Roslyn.Workbench.Mcp.ErrorReporting.Preparation;
using Roslyn.Workbench.Mcp.ErrorReporting.Projection;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class PreparedSubmissionExpiryIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_AcquiredSubmissionExpiresWhileConsentIsPending_WHEN_Confirming_THEN_ShouldRejectSubmission()
    {
        var now = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        var timeProvider = new Mock<TimeProvider>();
        var expirationTimer = new Mock<ITimer>();
        var policy = new Mock<IBoundedExpiringStorePolicy<string, PreparedSubmission>>();
        timeProvider.Setup(item => item.GetUtcNow()).Returns(() => now);
        timeProvider
            .Setup(item => item.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan))
            .Returns(expirationTimer.Object);
        expirationTimer
            .Setup(item => item.Change(It.IsAny<TimeSpan>(), Timeout.InfiniteTimeSpan))
            .Returns(true);
        policy.SetupGet(item => item.Capacity).Returns(5);
        policy
            .Setup(item => item.GetExpiration(It.IsAny<PreparedSubmission>()))
            .Returns((PreparedSubmission submission) => submission.ExpiresAt);
        using var entries = new BoundedExpiringStore<string, PreparedSubmission>(
            policy.Object,
            timeProvider.Object);
        var target = new PreparedSubmissionStore(entries);
        var submission = CreateSubmission(now);
        target.TryAdd(submission).Should().BeTrue();
        target.TryBeginSubmission(submission.Handle).Outcome.Should().Be(SubmissionAcquisitionOutcome.Acquired);

        now = submission.ExpiresAt;

        target.TryConfirmSubmission(submission.Handle).Should().BeFalse();
    }

    private static PreparedSubmission CreateSubmission(DateTimeOffset createdAt)
    {
        var report = new ExternalErrorReport
        {
            ReportId = "ReportId",
            FailureTime = createdAt,
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
        var payload = new PreparedDispatchPayload<string>
        {
            DispatcherName = "DispatcherName",
            Destination = "Destination",
            ReportId = "ReportId",
            Report = report,
            PreviewBytes = ImmutableArray.Create<byte>(1, 2, 3),
            PreviewJson = "PreviewJson",
            DispatchState = "DispatchState",
        };

        return new PreparedSubmission
        {
            Handle = "Handle",
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddMinutes(30),
            State = PreparedSubmissionState.Prepared,
            Payload = payload,
        };
    }
}
