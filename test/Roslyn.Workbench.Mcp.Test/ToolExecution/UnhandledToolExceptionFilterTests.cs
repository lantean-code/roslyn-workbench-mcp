using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class UnhandledToolExceptionFilterTests
{
    private readonly Mock<ILogger<UnhandledToolExceptionFilter>> _logger;
    private readonly UnhandledToolExceptionFilter _target;

    public UnhandledToolExceptionFilterTests()
    {
        _logger = new Mock<ILogger<UnhandledToolExceptionFilter>>();
        _target = new UnhandledToolExceptionFilter(_logger.Object);
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
    }

    [Fact]
    public async Task GIVEN_HandlerCancellation_WHEN_FilteringCall_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var context = CreateContext();
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromCanceled<CallToolResult>(cancellationSource.Token);

        var action = async () => await _target.InvokeAsync(next, context, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        VerifyNoLog();
    }

    [Fact]
    public async Task GIVEN_UnhandledToolException_WHEN_FilteringCall_THEN_ShouldLogMatchingCorrelationAndReturnSanitizedResult()
    {
        var context = CreateContext();
        var exception = new InvalidOperationException("Sensitive message");
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);
        var result = await _target.InvokeAsync(next, context, CancellationToken.None);

        result.Content.Should().BeEmpty();
        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structuredContent = result.StructuredContent.GetValueOrDefault();
        structuredContent.GetProperty("ok").GetBoolean().Should().BeFalse();
        var error = structuredContent.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("UnhandledException");
        error.GetProperty("message").GetString().Should().Be("Tool execution failed.");
        var correlationId = error.GetProperty("correlationId").GetString();
        correlationId.Should().NotBeNullOrWhiteSpace();
        structuredContent.GetRawText().Should().NotContain("Sensitive message");
        structuredContent.GetRawText().Should().NotContain(nameof(InvalidOperationException));
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
}
