using System.Text.Json;

using Roslyn.Workbench.Mcp.Test.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.CodeActions;

public sealed class CodeActionQueryMcpServerToolTests
{
    [Fact]
    public async Task GIVEN_ContextAcquisitionFailure_WHEN_InvokingQuery_THEN_ShouldPublishFailureWithoutCallingHandlerAndDisposeLease()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var workspaceLease = WorkspaceExecutionContextLease.Rejected(
            new WorkspaceExecutionFailure
            {
                Status = WorkspaceOperationStatus.Rejected,
                Error = new WorkspaceOperationError
                {
                    Code = "WorkspaceBusy",
                    Message = "Message",
                },
            },
            lease: operationLease.Object);
        var failure = CodeActionMcpServerToolTestData.CreateExecutionFailure(CodeActionExecutionOutcome.Rejected, "WorkspaceBusy");
        contextFactory
            .Setup(item => item.CreateQueryContext(
                It.Is<TestQueryRequest>(request => request.Name == "Name"),
                CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context: null, failure));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceBusy");
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestQueryRequest>(),
            It.IsAny<ICodeActionQueryContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AcquiredLeaseWithoutContext_WHEN_InvokingQuery_THEN_ShouldPublishUnhandledFailureAndDisposeLease()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context: null, failure: null));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        handler.Verify(item => item.ExecuteAsync(
            It.IsAny<TestQueryRequest>(),
            It.IsAny<ICodeActionQueryContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_WorkspaceSelectorArguments_WHEN_InvokingSuccessfulQuery_THEN_ShouldPassTypedRequestAndPublishResponse()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(
            new Mock<IWorkspaceExecutionContext>().Object,
            operationLease.Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(
                It.Is<TestQueryRequest>(request =>
                    request.Name == "Name"
                    && request.Workspace != null
                    && request.Workspace.WorkspaceId == "WorkspaceId"),
                CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context.Object, failure: null));
        handler
            .Setup(item => item.ExecuteAsync(
                It.Is<TestQueryRequest>(request => request.Name == "Name"),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<TestQueryResponse>.Success(new TestQueryResponse
            {
                Value = "Value",
            }));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(includeWorkspace: true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("value").GetString().Should().Be("Value");
        handler.Verify(item => item.ExecuteAsync(
            It.Is<TestQueryRequest>(request => request.Name == "Name"),
            context.Object,
            CancellationToken.None), Times.Once);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerNoChange_WHEN_InvokingQuery_THEN_ShouldPublishSuccessfulNullData()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context.Object, failure: null));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<TestQueryResponse>.NoChange());
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Conflict")]
    [InlineData("Faulted")]
    public async Task GIVEN_HandlerErrorOutcome_WHEN_InvokingQuery_THEN_ShouldPublishFailure(
        string outcomeName)
    {
        var outcome = Enum.Parse<CodeActionExecutionOutcome>(outcomeName);
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context.Object, failure: null));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new CodeActionExecutionResult<TestQueryResponse>
            {
                Outcome = outcome,
                Error = new CodeActionExecutionError
                {
                    Code = outcomeName,
                    Message = "Message",
                },
                RequiredAction = RequiredAction.Retry,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString().Should().Be(outcomeName);
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
    }

    [Fact]
    public async Task GIVEN_HandlerFailureWithoutError_WHEN_InvokingQuery_THEN_ShouldPublishUnhandledFailure()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context.Object, failure: null));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .ReturnsAsync(new CodeActionExecutionResult<TestQueryResponse>
            {
                Outcome = CodeActionExecutionOutcome.Faulted,
            });
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
    }

    [Fact]
    public async Task GIVEN_HandlerThrows_WHEN_InvokingQuery_THEN_ShouldPublishUnhandledFailureAndDisposeLease()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), CancellationToken.None))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context.Object, failure: null));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, CancellationToken.None))
            .Returns(ValueTask.FromException<CodeActionExecutionResult<TestQueryResponse>>(new InvalidOperationException("Message")));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HandlerCancellation_WHEN_InvokingQuery_THEN_ShouldPropagateCancellationAndDisposeLease()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var operationLease = new Mock<IAsyncDisposable>();
        operationLease.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var workspaceLease = WorkspaceExecutionContextLease.Acquired(new Mock<IWorkspaceExecutionContext>().Object, operationLease.Object);
        contextFactory
            .Setup(item => item.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), cancellationSource.Token))
            .Returns(new CodeActionQueryExecutionLease(workspaceLease, context.Object, failure: null));
        handler
            .Setup(item => item.ExecuteAsync(It.IsAny<TestQueryRequest>(), context.Object, cancellationSource.Token))
            .Returns(ValueTask.FromCanceled<CodeActionExecutionResult<TestQueryResponse>>(cancellationSource.Token));
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var action = async () => await target.InvokeArgumentsAsync(McpServerToolTestData.CreateArguments(), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        operationLease.Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingQuery_THEN_ShouldPublishUnhandledFailureWithoutAcquiringContext()
    {
        var handler = new Mock<ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse>>();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var target = CreateTarget(handler.Object, contextFactory.Object);

        var result = await target.InvokeArgumentsAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement(42),
        }, CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
        contextFactory.Verify(item => item.CreateQueryContext(
            It.IsAny<WorkspaceBoundRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CodeActionQueryMcpServerTool<TestQueryRequest, TestQueryResponse> CreateTarget(
        ICodeActionQueryToolHandler<TestQueryRequest, TestQueryResponse> handler,
        ICodeActionExecutionContextFactory contextFactory)
    {
        return new CodeActionQueryMcpServerTool<TestQueryRequest, TestQueryResponse>(
            McpServerToolTestData.CreateProtocolTool("test-code-action-query"),
            handler,
            contextFactory);
    }

    public sealed record TestQueryRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record TestQueryResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
