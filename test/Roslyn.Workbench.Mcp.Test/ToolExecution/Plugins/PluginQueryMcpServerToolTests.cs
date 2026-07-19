using System.Text.Json;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.Plugins;

public sealed class PluginQueryMcpServerToolTests
{
    [Fact]
    public async Task GIVEN_ContextAcquisitionFailure_WHEN_InvokingQuery_THEN_ShouldPublishFailureWithoutCallingHandlerAndDisposeLease()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var failure = PluginMcpServerToolTestData.CreateExecutionFailure(PluginExecutionOutcome.Rejected, "WorkspaceBusy");
        contextFactory
            .Setup(item => item.CreateQueryContext(
                It.Is<TestQueryRequest>(request => request.Name == "Name"),
                CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Rejected(failure, lease: operationLease.Object));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceBusy");
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestQueryRequest>(),
            It.IsAny<IQueryContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_WorkspaceSelectorArguments_WHEN_InvokingSuccessfulQuery_THEN_ShouldPassTypedRequestAndPublishResponse()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        contextFactory
            .Setup(item => item.CreateQueryContext(
                It.Is<TestQueryRequest>(request =>
                    request.Name == "Name"
                    && request.Workspace != null
                    && request.Workspace.WorkspaceId == "WorkspaceId"),
                CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object, operationLease.Object));
        handler
            .Setup(item => item.ExecuteAsync(
                It.Is<TestQueryRequest>(request => request.Name == "Name"),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<TestQueryResponse>.Success(new TestQueryResponse
            {
                Value = "Value",
            }));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(includeWorkspace: true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("data").GetProperty("value").GetString().Should().Be("Value");
        contextFactory.Verify(item => item.CreateQueryContext(
            It.Is<TestQueryRequest>(request => request.Workspace!.WorkspaceId == "WorkspaceId"),
            CancellationToken.None), Times.Once);
        handler.Verify(item => item.ExecuteAsync(
            It.Is<TestQueryRequest>(request => request.Name == "Name"),
            context.Object,
            CancellationToken.None), Times.Once);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerNoChange_WHEN_InvokingQuery_THEN_ShouldPublishSuccessfulNullData()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<TestQueryResponse>.NoChange());
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(PluginExecutionOutcome.Rejected, "Rejected")]
    [InlineData(PluginExecutionOutcome.Conflict, "Conflict")]
    [InlineData(PluginExecutionOutcome.Faulted, "Faulted")]
    public async Task GIVEN_HandlerErrorOutcome_WHEN_InvokingQuery_THEN_ShouldPublishFailure(
        PluginExecutionOutcome outcome,
        string code)
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new PluginExecutionResult<TestQueryResponse>
            {
                Outcome = outcome,
                Error = new PluginExecutionError
                {
                    Code = code,
                    Message = "Message",
                },
                RequiredAction = RequiredAction.Retry,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be(code);
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
    }

    [Fact]
    public async Task GIVEN_HandlerFailureWithoutError_WHEN_InvokingQuery_THEN_ShouldPropagateFailure()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new PluginExecutionResult<TestQueryResponse>
            {
                Outcome = PluginExecutionOutcome.Faulted,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_HandlerThrows_WHEN_InvokingQuery_THEN_ShouldPropagateFailureAndDisposeLease()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object, operationLease.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .Returns(() => ValueTask.FromException<PluginExecutionResult<TestQueryResponse>>(new InvalidOperationException("Message")));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerCancellation_WHEN_InvokingQuery_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object, operationLease.Object));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, cancellationSource.Token))
            .Returns(() => ValueTask.FromCanceled<PluginExecutionResult<TestQueryResponse>>(cancellationSource.Token));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingQuery_THEN_ShouldPropagateFailureWithoutAcquiringContext()
    {
        var handler = new Mock<IQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(42),
        }, CancellationToken.None);

        await action.Should().ThrowAsync<JsonException>();
        contextFactory.Verify(item => item.CreateQueryContext(
            It.IsAny<WorkspaceBoundRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestQueryRequest>(),
            It.IsAny<IQueryContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private PluginQueryMcpServerTool<TestQueryRequest, TestQueryResponse> CreateTarget(
        IQueryToolHandler<TestQueryRequest, TestQueryResponse> handler,
        IToolExecutionContextFactory contextFactory)
    {
        var registration = McpServerToolTestData.CreatePluginQueryRegistration(handler, "test-query");
        var protocolFactory = McpServerToolTestData.CreateProtocolFactory(
            McpServerToolTestData.CreateProtocolTool("test-query"));
        return new PluginQueryMcpServerTool<TestQueryRequest, TestQueryResponse>(
            registration,
            contextFactory,
            protocolFactory.Object,
            McpServerToolTestData.CreateOptions());
    }

#pragma warning disable CA1515 // Moq's dynamic proxy must access these closed-generic handler contracts.
    public sealed record TestQueryRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record TestQueryResponse
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1515
}
