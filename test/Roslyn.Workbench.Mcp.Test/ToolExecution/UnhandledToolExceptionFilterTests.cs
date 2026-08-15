using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class UnhandledToolExceptionFilterTests : IDisposable
{
    private readonly Mock<ILogger<UnhandledToolExceptionFilter>> _logger;
    private readonly Mock<IErrorCaptureService> _captureService;
    private readonly Mock<ICapturedErrorStore> _capturedErrorStore;
    private readonly Mock<IErrorReportingAvailabilityService> _availabilityService;
    private readonly AdhocWorkspace _roslynWorkspace;
    private readonly UnhandledToolExceptionFilter _target;

    public UnhandledToolExceptionFilterTests()
    {
        _roslynWorkspace = new AdhocWorkspace();
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
                It.IsAny<CapturedWorkspaceContext?>(),
                It.IsAny<Exception>()))
            .Returns((Guid correlationId, string _, IDictionary<string, JsonElement>? _, TimeSpan _, bool _, CapturedWorkspaceContext? workspaceContext, Exception _) =>
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
                    Workspace = workspaceContext,
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
    public async Task GIVEN_HostProtocolException_WHEN_FilteringCall_THEN_ShouldPropagateSameException()
    {
        var context = CreateContext();
        var exception = new RoslynWorkbenchMcpProtocolException(
            "Invalid protocol request",
            McpErrorCode.InvalidParams);
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var action = async () => await _target.InvokeAsync(next, context, CancellationToken.None);

        var assertion = await action.Should().ThrowAsync<McpProtocolException>();
        assertion.Which.Should().BeSameAs(exception);
        VerifyNoLog();
        VerifyNoCapture();
    }

    [Fact]
    public async Task GIVEN_NonHostProtocolException_WHEN_FilteringCall_THEN_ShouldCaptureFailure()
    {
        var context = CreateContext();
        var exception = new McpProtocolException("Sensitive protocol failure", McpErrorCode.InvalidParams);
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        AssertCapturedFailure(result, exception, "Sensitive protocol failure");
    }

    [Fact]
    public async Task GIVEN_NonProtocolMcpException_WHEN_FilteringCall_THEN_ShouldCaptureFailure()
    {
        var context = CreateContext();
        var exception = new McpException("Sensitive MCP failure");
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        AssertCapturedFailure(result, exception, "Sensitive MCP failure");
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

    [Fact]
    public async Task GIVEN_WorkspaceAttributedFailure_WHEN_FilteringCall_THEN_ShouldCaptureOriginalFailureAndAuthoritativeContext()
    {
        var context = CreateContext();
        var exception = new InvalidOperationException("Sensitive message");
        var attributedException = CreateAttributedException(exception);
        _availabilityService
            .Setup(item => item.GetAvailability(
                attributedException.WorkspaceContext.WorkspaceId,
                attributedException.WorkspaceContext.WorkspaceEpoch,
                null))
            .Returns(new ErrorReportingAvailability
            {
                State = ErrorReportingState.Available,
            });
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(attributedException);

        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        AssertCapturedFailure(
            result,
            exception,
            "Sensitive message",
            attributedException.WorkspaceContext);
    }

    [Fact]
    public async Task GIVEN_WorkspaceAttributedRequestCancellation_WHEN_FilteringCall_THEN_ShouldPropagateOriginalCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var context = CreateContext();
        var exception = new OperationCanceledException(cancellationSource.Token);
        var attributedException = CreateAttributedException(exception);
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(attributedException);

        var action = async () => await _target.InvokeAsync(next, context, cancellationSource.Token);

        var assertion = await action.Should().ThrowAsync<OperationCanceledException>();
        assertion.Which.Should().BeSameAs(exception);
        VerifyNoLog();
        VerifyNoCapture();
    }

    [Fact]
    public async Task GIVEN_WorkspaceAttributedHostProtocolFailure_WHEN_FilteringCall_THEN_ShouldPropagateOriginalFailure()
    {
        var context = CreateContext();
        var exception = new RoslynWorkbenchMcpProtocolException(
            "Invalid protocol request",
            McpErrorCode.InvalidParams);
        var attributedException = CreateAttributedException(exception);
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(attributedException);

        var action = async () => await _target.InvokeAsync(next, context, CancellationToken.None);

        var assertion = await action.Should().ThrowAsync<RoslynWorkbenchMcpProtocolException>();
        assertion.Which.Should().BeSameAs(exception);
        VerifyNoLog();
        VerifyNoCapture();
    }

    public void Dispose()
    {
        _roslynWorkspace.Dispose();
    }

    private void AssertCapturedFailure(
        CallToolResult result,
        Exception exception,
        string sensitiveMessage,
        CapturedWorkspaceContext? workspaceContext = null)
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
            workspaceContext,
            exception), Times.Once);
        _capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(
            record => record.CorrelationId == correlationId)), Times.Once);
        var workspaceId = workspaceContext?.WorkspaceId;
        var workspaceEpoch = workspaceContext?.WorkspaceEpoch;

        _availabilityService.Verify(item => item.GetAvailability(
            workspaceId,
            workspaceEpoch,
            null), Times.Once);
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

    private WorkspaceAttributedToolException CreateAttributedException(Exception exception)
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            WorkspaceEpoch = 5,
            LoadedPath = "C:\\Workspace\\Solution.sln",
            WorkspaceRoot = "C:\\Workspace",
        };
        var project = _roslynWorkspace.CurrentSolution.AddProject(
            "Project",
            "Project",
            LanguageNames.CSharp);
        var solution = project.Solution;
        var workspaceContext = new CapturedWorkspaceContext(
            workspaceIdentity,
            solution,
            transactionRevision: null);

        return new WorkspaceAttributedToolException(
            workspaceContext,
            exception);
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
                It.IsAny<CapturedWorkspaceContext?>(),
                It.IsAny<Exception>()),
            Times.Never);
        _capturedErrorStore.Verify(item => item.Add(It.IsAny<CapturedErrorRecord>()), Times.Never);
        _availabilityService.Verify(
            item => item.GetAvailability(It.IsAny<Guid?>(), It.IsAny<long?>(), It.IsAny<bool?>()),
            Times.Never);
    }
}
