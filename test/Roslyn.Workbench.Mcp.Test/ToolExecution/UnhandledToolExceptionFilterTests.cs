using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class UnhandledToolExceptionFilterTests
{
    private readonly Mock<ILogger<UnhandledToolExceptionFilter>> _logger;
    private readonly Mock<IErrorCaptureService> _captureService;
    private readonly Mock<ICapturedErrorStore> _capturedErrorStore;
    private readonly Mock<IErrorReportingAvailabilityService> _availabilityService;
    private readonly UnhandledToolExceptionFilter _target;

    public UnhandledToolExceptionFilterTests()
    {
        _logger = new Mock<ILogger<UnhandledToolExceptionFilter>>();
        _captureService = new Mock<IErrorCaptureService>();
        _capturedErrorStore = new Mock<ICapturedErrorStore>();
        _availabilityService = new Mock<IErrorReportingAvailabilityService>();
        _logger.Setup(item => item.IsEnabled(LogLevel.Error)).Returns(true);
        _captureService
            .Setup(item => item.Capture(
                It.IsAny<Guid>(),
                "tool-name",
                null,
                It.IsAny<TimeSpan>(),
                false,
                It.IsAny<Exception>()))
            .Returns((Guid correlationId, string _, IDictionary<string, JsonElement>? _, TimeSpan _, bool _, Exception _) =>
                new CapturedErrorRecord
                {
                    CorrelationId = correlationId,
                    FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                    ExpiresAt = DateTimeOffset.Parse("2000-01-01T01:00:00Z", CultureInfo.InvariantCulture),
                    ToolName = "tool-name",
                    ExecutionFamily = "Unknown",
                    PluginClassification = "Unknown",
                    DurationMilliseconds = 0,
                    ServerVersion = "ServerVersion",
                    RoslynVersion = "RoslynVersion",
                    DotNetVersion = "DotNetVersion",
                    OperatingSystem = "OperatingSystem",
                    ProcessorArchitecture = "ProcessorArchitecture",
                });
        _availabilityService
            .Setup(item => item.GetAvailability(null, null, null))
            .Returns(new ErrorReportingAvailability
            {
                State = ErrorReportingState.Available,
            });

        _target = new UnhandledToolExceptionFilter(
            _logger.Object,
            _captureService.Object,
            _capturedErrorStore.Object,
            _availabilityService.Object);
    }

    [Fact]
    public async Task GIVEN_HandlerSucceeds_WHEN_FilteringCall_THEN_ShouldReturnHandlerResult()
    {
        var expected = new CallToolResult
        {
            Content = [],
            IsError = false,
        };

        var context = CreateContext();
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) => ValueTask.FromResult(expected);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        result.Should().BeSameAs(expected);
        VerifyNoLog();
        VerifyNoCapture();
    }

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_FilteringCall_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var context = CreateContext();
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromCanceled<CallToolResult>(cancellationSource.Token);

        var action = async () => await _target.InvokeAsync(next, context, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        VerifyNoLog();
        VerifyNoCapture();
    }

    [Fact]
    public async Task GIVEN_UncancelledOperationCanceledException_WHEN_FilteringCall_THEN_ShouldCaptureFailure()
    {
        var context = CreateContext();
        var exception = new OperationCanceledException("Sensitive cancellation failure");
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        AssertCapturedFailure(result, exception, "Sensitive cancellation failure");
    }

    [Fact]
    public async Task GIVEN_UnrelatedTokenCancellation_WHEN_FilteringActiveRequest_THEN_ShouldCaptureFailure()
    {
        using var unrelatedCancellation = new CancellationTokenSource();
        await unrelatedCancellation.CancelAsync();
        var context = CreateContext();
        var exception = new OperationCanceledException(
            "Sensitive unrelated cancellation",
            innerException: null,
            unrelatedCancellation.Token);
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        AssertCapturedFailure(result, exception, "Sensitive unrelated cancellation");
    }

    [Fact]
    public async Task GIVEN_UnhandledToolException_WHEN_FilteringCall_THEN_ShouldLogMatchingCorrelationAndReturnSanitizedResult()
    {
        var context = CreateContext();
        var exception = new InvalidOperationException("Sensitive message");
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        AssertCapturedFailure(result, exception, "Sensitive message");
    }

    private void AssertCapturedFailure(
        CallToolResult result,
        Exception exception,
        string sensitiveMessage)
    {
        result.Content.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structuredContent = result.StructuredContent.GetValueOrDefault();
        structuredContent.GetProperty("ok").GetBoolean().Should().BeFalse();
        var error = structuredContent.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("UnhandledException");
        error.GetProperty("message").GetString().Should().Be("Tool execution failed.");
        var correlationId = error.GetProperty("correlationId").GetGuid();
        correlationId.Should().NotBeEmpty();
        structuredContent.GetRawText().Should().NotContain(sensitiveMessage);
        structuredContent.GetRawText().Should().NotContain(exception.GetType().Name);
        structuredContent.GetProperty("diagnostics").GetProperty("detailsAvailable").GetBoolean().Should().BeTrue();
        structuredContent.GetProperty("reporting").GetProperty("state").GetString().Should().Be("Available");
        _captureService.Verify(item => item.Capture(
            correlationId,
            "tool-name",
            null,
            It.IsAny<TimeSpan>(),
            false,
            exception), Times.Once);
        _capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(
            record => record.CorrelationId == correlationId)), Times.Once);
        _availabilityService.Verify(item => item.GetAvailability(null, null, null), Times.Once);
        _logger.Verify(
            item => item.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString() ==
                    $"Unhandled exception while executing MCP tool tool-name. Correlation ID: {correlationId}"),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static RequestContext<CallToolRequestParams> CreateContext()
    {
        var server = new Mock<McpServer>();
        return new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest
            {
                Method = "tools/call",
            },
            new CallToolRequestParams
            {
                Name = "tool-name",
            });
    }

    private void VerifyNoLog()
    {
        _logger.Verify(
            item => item.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private void VerifyNoCapture()
    {
        _captureService.Verify(
            item => item.Capture(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, JsonElement>?>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>(),
                It.IsAny<Exception>()),
            Times.Never);
        _capturedErrorStore.Verify(item => item.Add(It.IsAny<CapturedErrorRecord>()), Times.Never);
        _availabilityService.Verify(
            item => item.GetAvailability(It.IsAny<Guid?>(), It.IsAny<long?>(), It.IsAny<bool?>()),
            Times.Never);
    }
}
