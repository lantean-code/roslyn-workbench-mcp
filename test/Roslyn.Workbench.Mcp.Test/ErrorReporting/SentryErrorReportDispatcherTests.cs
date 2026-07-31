using System.Text;
using System.Text.Json;
using Sentry;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class SentryErrorReportDispatcherTests
{
    private const string _destination = "Sentry project 1000000000000000 at o100000.ingest.us.sentry.io";
    private const string _dsn = "https://0123456789abcdef0123456789abcdef@o100000.ingest.us.sentry.io/1000000000000000";

    [Fact]
    public void GIVEN_ProviderNeutralReport_WHEN_CreatingSentryPayload_THEN_ShouldCreateRepresentativeReviewPayload()
    {
        var client = new Mock<ISentryClient>();
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var target = new SentryErrorReportDispatcher(client.Object, configuration);
        var report = CreateReport();

        var result = target.CreatePayload(report);

        result.DispatcherName.Should().Be("Sentry");
        result.Destination.Should().Be(_destination);
        result.ReportId.Should().Be(report.ReportId);
        result.Report.Should().BeSameAs(report);
        Encoding.UTF8.GetString(result.PreviewBytes.AsSpan()).Should().Be(result.Preview.GetRawText());
        result.Preview.TryGetProperty("event_id", out _).Should().BeFalse();
        result.Preview.GetProperty("fingerprint").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            "roslyn-workbench",
            report.Tool,
            report.ExceptionClassification,
            report.ExecutionFamily,
            "RoslynWorkbench",
            "Roslyn");
        var message = result.Preview.GetProperty("logentry");
        message.GetProperty("message").GetString().Should().Be("Roslyn Workbench reported {0} in {1}.");
        message.GetProperty("params").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            report.ExceptionClassification,
            report.Tool);
        message.GetProperty("formatted").GetString().Should().Be(
            $"Roslyn Workbench reported {report.ExceptionClassification} in {report.Tool}.");
        result.Preview
            .GetProperty("contexts")
            .GetProperty("roslyn_workbench")
            .GetProperty("schemaVersion")
            .GetInt32()
            .Should()
            .Be(ExternalErrorReport.CurrentSchemaVersion);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GIVEN_PreparedPayload_WHEN_Dispatching_THEN_ShouldCaptureStronglyTypedEventThroughSentrySdk()
    {
        SentryEvent? capturedEvent = null;
        var client = new Mock<ISentryClient>();
        var report = CreateReport();
        var expectedEventId = SentryId.Parse("fedcba9876543210fedcba9876543210");
        client
            .Setup(item => item.CaptureEvent(
                It.IsAny<SentryEvent>(),
                It.IsAny<Scope?>(),
                It.IsAny<SentryHint?>()))
            .Callback<SentryEvent, Scope?, SentryHint?>((sentryEvent, _, _) =>
            {
                capturedEvent = sentryEvent;
            })
            .Returns(expectedEventId);
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var target = new SentryErrorReportDispatcher(client.Object, configuration);
        var payload = target.CreatePayload(report);

        var result = await target.DispatchAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Accepted);
        result.ReportReference.Should().Be(expectedEventId.ToString());
        capturedEvent.Should().NotBeNull();
        capturedEvent.Should().BeOfType<SentryEvent>();
        capturedEvent.Message!.Message.Should().Be("Roslyn Workbench reported {0} in {1}.");
        capturedEvent.Message.Params.Should().Equal(report.ExceptionClassification, report.Tool);
        capturedEvent.Message.Formatted.Should().Be(
            $"Roslyn Workbench reported {report.ExceptionClassification} in {report.Tool}.");
        capturedEvent.Fingerprint.Should().Equal(
            "roslyn-workbench",
            report.Tool,
            report.ExceptionClassification,
            report.ExecutionFamily,
            "RoslynWorkbench",
            "Roslyn");
        capturedEvent.Contexts.TryGetValue("roslyn_workbench", out var workbenchContext).Should().BeTrue();
        JsonSerializer.SerializeToElement(workbenchContext)
            .GetProperty("schemaVersion")
            .GetInt32()
            .Should()
            .Be(ExternalErrorReport.CurrentSchemaVersion);
        client.Verify(item => item.CaptureEvent(
            It.IsAny<SentryEvent>(),
            It.IsAny<Scope?>(),
            It.IsAny<SentryHint?>()), Times.Once);
        client.Verify(item => item.FlushAsync(It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SdkRejectsPreparedEvent_WHEN_Dispatching_THEN_ShouldReturnRejectedWithoutFlush()
    {
        var client = new Mock<ISentryClient>();
        client
            .Setup(item => item.CaptureEvent(
                It.IsAny<SentryEvent>(),
                It.IsAny<Scope?>(),
                It.IsAny<SentryHint?>()))
            .Returns(SentryId.Empty);
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var target = new SentryErrorReportDispatcher(client.Object, configuration);
        var payload = target.CreatePayload(CreateReport());

        var result = await target.DispatchAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ErrorCode.Should().Be("SentryCaptureRejected");
        client.Verify(item => item.FlushAsync(It.IsAny<TimeSpan>()), Times.Never);
    }

    private static ExternalErrorReport CreateReport()
    {
        return new ExternalErrorReport
        {
            ReportId = "0123456789abcdef0123456789abcdef",
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            Tool = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 25,
            ExceptionClassification = "System.InvalidOperationException",
            StackFrames =
            [
                new ExternalStackFrame { Component = "RoslynWorkbench" },
                new ExternalStackFrame { Component = "Roslyn" },
            ],
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }
}
