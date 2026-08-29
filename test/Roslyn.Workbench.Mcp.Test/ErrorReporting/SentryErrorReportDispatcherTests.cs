using System.Collections.Immutable;
using System.Security.Cryptography;
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
        Encoding.UTF8.GetString(result.PreviewBytes.AsSpan()).Should().Be(result.PreviewJson);
        var preparedPayload = (PreparedDispatchPayload<SentryEvent>)result;
        using var preview = JsonDocument.Parse(result.PreviewJson);
        var previewRoot = preview.RootElement;
        previewRoot.GetProperty("event_id").GetString().Should().Be(preparedPayload.DispatchState.EventId.ToString());
        previewRoot.GetProperty("timestamp").GetDateTimeOffset().Should().Be(preparedPayload.DispatchState.Timestamp);
        previewRoot.GetProperty("fingerprint").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            "roslyn-workbench",
            report.Tool,
            report.ExceptionClassification,
            report.ExecutionFamily,
            "RoslynWorkbench",
            "Roslyn");
        var message = previewRoot.GetProperty("logentry");
        message.GetProperty("message").GetString().Should().Be("Roslyn Workbench reported {0} in {1}.");
        message.GetProperty("params").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            report.ExceptionClassification,
            report.Tool);
        message.GetProperty("formatted").GetString().Should().Be(
            $"Roslyn Workbench reported {report.ExceptionClassification} in {report.Tool}.");
        var exception = previewRoot.GetProperty("exception").GetProperty("values")[0];
        exception.GetProperty("type").GetString().Should().Be("System.InvalidOperationException");
        exception.GetProperty("value").GetString().Should().Be("Message");
        exception.GetProperty("stacktrace").GetProperty("frames").GetArrayLength().Should().Be(2);
        var workbenchContext = previewRoot
            .GetProperty("contexts")
            .GetProperty("roslyn_workbench");
        workbenchContext.GetProperty("schemaVersion").GetInt32()
            .Should().Be(ExternalErrorReport.CurrentSchemaVersion);
        workbenchContext
            .GetProperty("exceptions")[0]
            .GetProperty("stackFrames")[0]
            .GetProperty("component")
            .GetString()
            .Should().Be("RoslynWorkbench");
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

        var result = await target.DispatchAsync(
            payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Accepted);
        result.ReportReference.Should().Be(expectedEventId.ToString());
        result.PayloadDigest.Should().Be(CalculateDigest(payload.PreviewBytes));
        capturedEvent.Should().NotBeNull();
        capturedEvent.Should().BeOfType<SentryEvent>();
        var preparedEventPayload = (PreparedDispatchPayload<SentryEvent>)payload;
        capturedEvent.Should().NotBeSameAs(preparedEventPayload.DispatchState);
        capturedEvent.EventId.Should().Be(preparedEventPayload.DispatchState.EventId);
        capturedEvent.Timestamp.Should().Be(preparedEventPayload.DispatchState.Timestamp);
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
    public async Task GIVEN_PreparedSentryPayload_WHEN_DispatchingWithoutMessages_THEN_ShouldCaptureRedactedEvent()
    {
        SentryEvent? capturedEvent = null;
        var client = new Mock<ISentryClient>();
        client
            .Setup(item => item.CaptureEvent(
                It.IsAny<SentryEvent>(),
                It.IsAny<Scope?>(),
                It.IsAny<SentryHint?>()))
            .Callback<SentryEvent, Scope?, SentryHint?>((sentryEvent, _, _) =>
            {
                capturedEvent = sentryEvent;
            })
            .Returns(SentryId.Parse("fedcba9876543210fedcba9876543210"));
        var target = new SentryErrorReportDispatcher(
            client.Object,
            new SentryProviderConfiguration(_dsn, _destination));
        var original = target.CreatePayload(CreateReport());

        var result = await target.DispatchAsync(
            original,
            ExceptionMessageHandling.Remove,
            CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Accepted);
        capturedEvent.Should().NotBeNull();
        var dispatchedBytes = SentryEventJsonSerializer.Serialize(capturedEvent!);
        result.PayloadDigest.Should().Be(CalculateDigest(dispatchedBytes));
        using var preview = JsonDocument.Parse(dispatchedBytes.ToArray());
        var root = preview.RootElement;
        var sentryException = root.GetProperty("exception").GetProperty("values")[0];
        sentryException.TryGetProperty("value", out _).Should().BeFalse();
        var reportException = root.GetProperty("contexts").GetProperty("roslyn_workbench")
            .GetProperty("exceptions")[0];
        reportException.TryGetProperty("message", out _).Should().BeFalse();
        sentryException.GetProperty("stacktrace").GetProperty("frames").GetArrayLength().Should().Be(2);
        original.Report.Exceptions[0].Message.Should().Be("Message");
    }

    [Fact]
    public async Task GIVEN_NonSentryPayload_WHEN_DispatchingWithoutMessages_THEN_ShouldRejectIt()
    {
        var target = new SentryErrorReportDispatcher(
            new Mock<ISentryClient>().Object,
            new SentryProviderConfiguration(_dsn, _destination));
        var report = CreateReport();
        var payload = new PreparedDispatchPayload<string>
        {
            DispatcherName = "Sentry",
            Destination = _destination,
            ReportId = report.ReportId,
            Report = report,
            PreviewBytes = [],
            PreviewJson = "{}",
            DispatchState = "invalid",
        };

        var result = await target.DispatchAsync(
            payload,
            ExceptionMessageHandling.Remove,
            CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ErrorCode.Should().Be("InvalidPreparedErrorReport");
    }

    [Fact]
    public void GIVEN_ExceptionWithoutFrames_WHEN_CreatingPayload_THEN_ShouldOmitStackTrace()
    {
        var target = new SentryErrorReportDispatcher(
            new Mock<ISentryClient>().Object,
            new SentryProviderConfiguration(_dsn, _destination));
        var report = CreateReport() with
        {
            Exceptions =
            [
                new ExternalException
                {
                    Type = "System.InvalidOperationException",
                    Message = "Message",
                },
            ],
        };

        var result = target.CreatePayload(report);

        using var preview = JsonDocument.Parse(result.PreviewJson);
        var exception = preview.RootElement.GetProperty("exception").GetProperty("values")[0];
        exception.TryGetProperty("stacktrace", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NestedExceptions_WHEN_CreatingPayload_THEN_ShouldUseSentryChainOrder()
    {
        var target = new SentryErrorReportDispatcher(
            new Mock<ISentryClient>().Object,
            new SentryProviderConfiguration(_dsn, _destination));
        var report = CreateReport() with
        {
            Exceptions =
            [
                new ExternalException
                {
                    Type = "OuterException",
                    Message = "OuterMessage",
                },
                new ExternalException
                {
                    Type = "InnerException",
                    Message = "InnerMessage",
                },
            ],
        };

        var result = target.CreatePayload(report);

        using var preview = JsonDocument.Parse(result.PreviewJson);
        var root = preview.RootElement;
        root.GetProperty("exception").GetProperty("values").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .Should().Equal("InnerException", "OuterException");
        root.GetProperty("contexts").GetProperty("roslyn_workbench").GetProperty("exceptions").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .Should().Equal("OuterException", "InnerException");
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

        var result = await target.DispatchAsync(
            payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ErrorCode.Should().Be("SentryCaptureRejected");
        client.Verify(item => item.FlushAsync(It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreparedStateForDifferentDispatcher_WHEN_Dispatching_THEN_ShouldRejectWithoutCapture()
    {
        var client = new Mock<ISentryClient>();
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var target = new SentryErrorReportDispatcher(client.Object, configuration);
        var prepared = target.CreatePayload(CreateReport());
        var payload = new PreparedDispatchPayload<string>
        {
            DispatcherName = prepared.DispatcherName,
            Destination = prepared.Destination,
            ReportId = prepared.ReportId,
            Report = prepared.Report,
            PreviewBytes = prepared.PreviewBytes,
            PreviewJson = prepared.PreviewJson,
            DispatchState = "DispatchState",
        };

        var result = await target.DispatchAsync(
            payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ErrorCode.Should().Be("InvalidPreparedErrorReport");
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GIVEN_UnsupportedMessageHandling_WHEN_Dispatching_THEN_ShouldRejectWithoutCapture()
    {
        var client = new Mock<ISentryClient>();
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var target = new SentryErrorReportDispatcher(client.Object, configuration);
        var payload = target.CreatePayload(CreateReport());

        var result = await target.DispatchAsync(
            payload,
            (ExceptionMessageHandling)int.MaxValue,
            CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ErrorCode.Should().Be("InvalidExceptionMessageHandling");
        client.VerifyNoOtherCalls();
    }

    private static string CalculateDigest(ImmutableArray<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan()));
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
            Exceptions =
            [
                new ExternalException
                {
                    Type = "System.InvalidOperationException",
                    Message = "Message",
                    StackFrames =
                    [
                        new ExternalStackFrame { Component = ErrorReportComponent.RoslynWorkbench },
                        new ExternalStackFrame { Component = ErrorReportComponent.Roslyn },
                    ],
                },
            ],
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }
}
