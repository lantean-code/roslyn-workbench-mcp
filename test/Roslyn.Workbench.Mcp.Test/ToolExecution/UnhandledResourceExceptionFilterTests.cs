using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class UnhandledResourceExceptionFilterTests
{
    private readonly Mock<ILogger<UnhandledResourceExceptionFilter>> _logger = new Mock<ILogger<UnhandledResourceExceptionFilter>>();
    private readonly Mock<IErrorCaptureService> _captureService = new Mock<IErrorCaptureService>();
    private readonly Mock<ICapturedErrorStore> _store = new Mock<ICapturedErrorStore>();
    private readonly Mock<IErrorReportingAvailabilityService> _availability = new Mock<IErrorReportingAvailabilityService>();
    private readonly UnhandledResourceExceptionFilter _target;

    public UnhandledResourceExceptionFilterTests()
    {
        _logger.Setup(item => item.IsEnabled(LogLevel.Error)).Returns(true);
        _captureService.Setup(item => item.CaptureResourceRead(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<Exception>()))
            .Returns((Guid id, TimeSpan duration, bool cancelled, Exception _) => new CapturedErrorRecord
            {
                CorrelationId = id,
                FailureTime = DateTimeOffset.UnixEpoch,
                ExpiresAt = DateTimeOffset.UnixEpoch.AddHours(1),
                ToolName = "resources/read",
                ExecutionFamily = "Resource",
                PluginClassification = "Host",
                DurationMilliseconds = (long)duration.TotalMilliseconds,
                CancellationRequested = cancelled,
                ServerVersion = "test",
                RoslynVersion = "test",
                DotNetVersion = "test",
                OperatingSystem = "test",
                ProcessorArchitecture = "test",
            });
        _availability.Setup(item => item.GetAvailability(null, null, It.IsAny<bool?>()))
            .Returns(new ErrorReportingAvailability { State = ErrorReportingState.Available, CanPrepare = true });
        _target = new UnhandledResourceExceptionFilter(_logger.Object, _captureService.Object, _store.Object, _availability.Object);
    }

    [Fact]
    public async Task GIVEN_SuccessfulRead_WHEN_Filtering_THEN_ShouldReturnOriginalResultWithoutCapture()
    {
        var expected = new ReadResourceResult { Contents = [] };
        var context = CreateContext();
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next = (_, _) => ValueTask.FromResult(expected);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        result.Should().BeSameAs(expected);
        VerifyNoCapture();
    }

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_Filtering_THEN_ShouldPropagateCancellationWithoutCapture()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = CreateContext();
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next = (_, _) => ValueTask.FromCanceled<ReadResourceResult>(cancellation.Token);
        var action = async () => await _target.InvokeAsync(next, context, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        VerifyNoCapture();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_DeliberateProtocolFailure_WHEN_Filtering_THEN_ShouldPreserveOriginalException(bool hostRejection)
    {
        var context = CreateContext();
        McpProtocolException exception = hostRejection
            ? new RoslynWorkbenchMcpProtocolException("Invalid request", McpErrorCode.InvalidParams)
            : new McpProtocolException("Unknown resource", McpErrorCode.ResourceNotFound);
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next = (_, _) => ValueTask.FromException<ReadResourceResult>(exception);
        var action = async () => await _target.InvokeAsync(next, context, CancellationToken.None);

        var failure = await action.Should().ThrowAsync<McpProtocolException>();
        failure.Which.Should().BeSameAs(exception);
        VerifyNoCapture();
    }

    [Fact]
    public async Task GIVEN_CancelledRequestWithUnexpectedFailure_WHEN_Filtering_THEN_ShouldRetainTheFailureAndCancellationState()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = CreateContext();
        var exception = new InvalidOperationException("Unexpected failure during cancellation");
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next = (_, _) => ValueTask.FromException<ReadResourceResult>(exception);
        var action = async () => await _target.InvokeAsync(next, context, cancellation.Token);

        var failure = await action.Should().ThrowAsync<McpProtocolException>();
        failure.Which.ErrorCode.Should().Be(McpErrorCode.InternalError);
        _captureService.Verify(item => item.CaptureResourceRead(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), true, exception), Times.Once);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task GIVEN_UnexpectedFailure_WHEN_Filtering_THEN_ShouldReturnSanitisedCorrelatedDiagnostics(bool? supportsElicitation, int failureKind)
    {
        var context = CreateContext(supportsElicitation);
        Exception exception = failureKind switch
        {
            1 => new OperationCanceledException("Sensitive failure"),
            2 => new McpProtocolException("Sensitive failure", McpErrorCode.InvalidParams),
            _ => new InvalidOperationException("Sensitive failure"),
        };
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next = (_, _) => ValueTask.FromException<ReadResourceResult>(exception);
        var action = async () => await _target.InvokeAsync(next, context, CancellationToken.None);

        var failure = await action.Should().ThrowAsync<McpProtocolException>();
        failure.Which.ErrorCode.Should().Be(McpErrorCode.InternalError);
        failure.Which.Message.Should().NotContain("Sensitive");
        failure.Which.InnerException.Should().BeNull();
        var error = ((JsonElement)failure.Which.Data["error"]!).Clone();
        var id = error.GetProperty("correlationId").GetGuid();
        error.GetProperty("code").GetString().Should().Be("UnhandledException");
        error.GetProperty("message").GetString().Should().Be("Resource read failed.");
        failure.Which.Message.Should().Contain(id.ToString());
        var diagnostics = (JsonElement)failure.Which.Data["diagnostics"]!;
        diagnostics.GetProperty("detailsTool").GetString().Should().Be("get-error-details");
        diagnostics.GetProperty("detailsAvailable").GetBoolean().Should().BeTrue();
        var reporting = (JsonElement)failure.Which.Data["reporting"]!;
        reporting.GetProperty("canPrepare").GetBoolean().Should().BeTrue();
        _captureService.Verify(item => item.CaptureResourceRead(id, It.IsAny<TimeSpan>(), false, exception), Times.Once);
        _store.Verify(item => item.Add(It.Is<CapturedErrorRecord>(record => record.CorrelationId == id)), Times.Once);
        _availability.Verify(item => item.GetAvailability(null, null, supportsElicitation), Times.Once);
        _logger.Verify(item => item.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state != null && state.ToString()!.Contains(id.ToString(), StringComparison.Ordinal)),
            exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private static RequestContext<ReadResourceRequestParams> CreateContext(bool? supportsElicitation = null)
    {
        var server = new Mock<McpServer>();
        if (supportsElicitation.HasValue)
        {
            var capabilities = new ClientCapabilities
            {
                Elicitation = supportsElicitation.Value ? new ElicitationCapability() : null,
            };
            server.SetupGet(item => item.ClientCapabilities).Returns(capabilities);
        }

        var request = new JsonRpcRequest { Method = "resources/read" };
        var parameters = new ReadResourceRequestParams { Uri = "skill://roslyn-workbench/SKILL.md" };
        return new RequestContext<ReadResourceRequestParams>(server.Object, request, parameters);
    }

    private void VerifyNoCapture()
    {
        _captureService.Verify(item => item.CaptureResourceRead(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<Exception>()), Times.Never);
        _store.Verify(item => item.Add(It.IsAny<CapturedErrorRecord>()), Times.Never);
    }
}
