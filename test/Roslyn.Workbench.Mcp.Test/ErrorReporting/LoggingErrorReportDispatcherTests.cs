using System.Text;
using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class LoggingErrorReportDispatcherTests
{
    [Fact]
    public void GIVEN_ProviderNeutralReport_WHEN_CreatingLoggingPayload_THEN_ShouldCreateRepresentativeReviewPayload()
    {
        var logger = new Mock<ILogger<LoggingErrorReportDispatcher>>();
        var target = new LoggingErrorReportDispatcher(logger.Object);
        var report = CreateReport();

        var result = target.CreatePayload(report);

        result.DispatcherName.Should().Be("Logging");
        result.Destination.Should().Be("standard error (stderr)");
        result.ReportId.Should().Be(report.ReportId);
        result.Report.Should().BeSameAs(report);
        Encoding.UTF8.GetString(result.PreviewBytes.AsSpan()).Should().Be(result.Preview.GetRawText());
        result.Preview.GetProperty("level").GetString().Should().Be("error");
        result.Preview.GetProperty("logger").GetString().Should().Be("roslyn-workbench-mcp");
        result.Preview.GetProperty("message").GetString().Should().Be(
            $"Roslyn Workbench reported {report.ExceptionClassification} in {report.Tool}.");
        result.Preview.GetProperty("report").GetProperty("reportId").GetString().Should().Be(report.ReportId);
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GIVEN_PreparedPayload_WHEN_Dispatching_THEN_ShouldLogSanitisedReportAndAcceptIt()
    {
        var logger = new Mock<ILogger<LoggingErrorReportDispatcher>>();
        logger.Setup(item => item.IsEnabled(LogLevel.Error)).Returns(true);
        var target = new LoggingErrorReportDispatcher(logger.Object);
        var report = CreateReport();
        var payload = target.CreatePayload(report);

        var result = await target.DispatchAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Accepted);
        result.ReportReference.Should().Be(report.ReportId);
        logger.Verify(item => item.Log(
            LogLevel.Error,
            It.Is<EventId>(eventId => eventId.Id == 1),
            It.Is<It.IsAnyType>((state, _) => ContainsReport(state.ToString(), report)),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(item => item.IsEnabled(LogLevel.Error), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MismatchedPreparedReport_WHEN_Dispatching_THEN_ShouldRejectWithoutLogging()
    {
        var logger = new Mock<ILogger<LoggingErrorReportDispatcher>>();
        var target = new LoggingErrorReportDispatcher(logger.Object);
        var report = CreateReport();
        var prepared = target.CreatePayload(report);
        var payload = prepared with { ReportId = "different-report-id" };

        var result = await target.DispatchAsync(payload, CancellationToken.None);

        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ErrorCode.Should().Be("InvalidPreparedErrorReport");
        logger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GIVEN_CancelledDispatch_WHEN_Dispatching_THEN_ShouldPropagateCancellationWithoutLogging()
    {
        var logger = new Mock<ILogger<LoggingErrorReportDispatcher>>();
        var target = new LoggingErrorReportDispatcher(logger.Object);
        var payload = target.CreatePayload(CreateReport());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await target.DispatchAsync(payload, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        logger.VerifyNoOtherCalls();
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

    private static bool ContainsReport(string? message, ExternalErrorReport report)
    {
        return message is not null
            && message.Contains(report.ReportId, StringComparison.Ordinal)
            && message.Contains(report.Tool, StringComparison.Ordinal);
    }
}
