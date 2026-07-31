using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class PreparedSubmissionStoreTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture);
    private readonly Mock<TimeProvider> _timeProvider;

    public PreparedSubmissionStoreTests()
    {
        _timeProvider = new Mock<TimeProvider>();
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now);
    }

    [Fact]
    public void GIVEN_PreparedSubmission_WHEN_TwoCallersAcquire_THEN_ShouldAllowOnlyOneSender()
    {
        var target = CreateTarget();
        target.TryAdd(CreateSubmission("Handle")).Should().BeTrue();

        var first = target.TryBeginSubmission("Handle");
        var second = target.TryBeginSubmission("Handle");

        first.Outcome.Should().Be(SubmissionAcquisitionOutcome.Acquired);
        second.Outcome.Should().Be(SubmissionAcquisitionOutcome.InProgress);
    }

    [Fact]
    public void GIVEN_SentSubmission_WHEN_Retrying_THEN_ShouldReturnOriginalReceipt()
    {
        var target = CreateTarget();
        target.TryAdd(CreateSubmission("Handle")).Should().BeTrue();
        target.TryBeginSubmission("Handle").Outcome.Should().Be(SubmissionAcquisitionOutcome.Acquired);
        var receipt = new ErrorSubmissionReceipt
        {
            Dispatcher = "Dispatcher",
            ReportReference = "ReportReference",
            PayloadDigest = "PayloadDigest",
        };
        target.Complete("Handle", receipt);

        var result = target.TryBeginSubmission("Handle");

        result.Outcome.Should().Be(SubmissionAcquisitionOutcome.AlreadySent);
        result.Submission!.Receipt.Should().Be(receipt);
    }

    [Fact]
    public void GIVEN_ExpiredSubmission_WHEN_Accessing_THEN_ShouldRemoveIt()
    {
        var target = CreateTarget();
        target.TryAdd(CreateSubmission("Handle")).Should().BeTrue();
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now.AddHours(1));

        var found = target.TryGet("Handle", out _);

        found.Should().BeFalse();
    }

    private PreparedSubmissionStore CreateTarget()
    {
        return new PreparedSubmissionStore(
            Options.Create(new ErrorReportingOptions()),
            _timeProvider.Object);
    }

    private PreparedSubmission CreateSubmission(string handle)
    {
        return new PreparedSubmission
        {
            Handle = handle,
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = _now,
            ExpiresAt = _now.AddMinutes(30),
            State = PreparedSubmissionState.Prepared,
            Payload = new PreparedDispatchPayload
            {
                DispatcherName = "Dispatcher",
                Destination = "Destination",
                ReportId = "ReportId",
                Report = new ExternalErrorReport
                {
                    ReportId = "ReportId",
                    FailureTime = _now,
                    Tool = "server-status",
                    ExecutionFamily = "ServerOwned",
                    PluginClassification = "Host",
                    DurationMilliseconds = 25,
                    ExceptionClassification = "DotNetException",
                    ServerVersion = "ServerVersion",
                    RoslynVersion = "RoslynVersion",
                    DotNetVersion = "DotNetVersion",
                    OperatingSystem = "Linux",
                    ProcessorArchitecture = "X64",
                },
                PreviewBytes = ImmutableArray.Create<byte>(1, 2, 3),
                Preview = JsonSerializer.SerializeToElement(new { value = "Value" }),
            },
        };
    }
}
