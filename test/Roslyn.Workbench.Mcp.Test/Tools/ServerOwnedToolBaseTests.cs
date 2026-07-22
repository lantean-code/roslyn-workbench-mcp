using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class ServerOwnedToolBaseTests
{
    [Fact]
    public void GIVEN_ReadOnlyTool_WHEN_CreatingTool_THEN_ShouldRequestExpectedProtocolMetadata()
    {
        var protocolTool = new Tool
        {
            Name = "workspace-list",
        };
        var protocolFactory = new Mock<IMcpToolProtocolFactory>();
        protocolFactory
            .Setup(item => item.CreateServerOwnedTool<WorkspaceListRequest, WorkspaceListData>(
                "workspace-list",
                "Workspace List",
                "Lists the currently loaded workspaces.",
                true,
                false,
                null,
                ToolOutputSchemaMode.Omit))
            .Returns(protocolTool);
        var service = new Mock<IWorkspaceLifecycleService>();
        var target = new WorkspaceListTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            service.Object);

        target.ProtocolTool.Should().BeSameAs(protocolTool);
        target.Metadata.Should().BeEmpty();
        protocolFactory.VerifyAll();
    }

    [Fact]
    public void GIVEN_DestructiveTool_WHEN_CreatingTool_THEN_ShouldRequestExpectedProtocolMetadata()
    {
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var service = new Mock<ITransactionService>();
        var target = new TransactionCommitTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            service.Object);

        protocolFactory.Verify(item => item.CreateServerOwnedTool<TransactionCommitRequest, TransactionCommitData>(
            "transaction-commit",
            "Transaction Commit",
            "Commits the current staged transaction to disk.",
            false,
            true,
            null,
            ToolOutputSchemaMode.Omit), Times.Once);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Conflict")]
    [InlineData("Faulted")]
    public async Task GIVEN_WorkspaceFailure_WHEN_InvokingTool_THEN_ShouldPublishStructuredFailure(
        string statusName)
    {
        var status = Enum.Parse<WorkspaceOperationStatus>(statusName);
        var error = new WorkspaceOperationError
        {
            Code = statusName,
            Message = "Message",
            RequiredAction = RequiredAction.Retry,
        };
        var serviceResult = status switch
        {
            WorkspaceOperationStatus.Rejected => WorkspaceOperationResult<WorkspaceListOutcome>.Rejected(error),
            WorkspaceOperationStatus.Conflict => WorkspaceOperationResult<WorkspaceListOutcome>.Conflict(error),
            WorkspaceOperationStatus.Faulted => WorkspaceOperationResult<WorkspaceListOutcome>.Faulted(error),
            _ => throw new ArgumentOutOfRangeException(nameof(statusName), statusName, "A failure status is required."),
        };
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.ListAsync(CancellationToken.None))
            .ReturnsAsync(serviceResult);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-list",
            cancellationToken: CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be(statusName);
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
    }

    [Fact]
    public async Task GIVEN_ServiceCancellation_WHEN_InvokingTool_THEN_ShouldPropagateCancellation()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        service
            .Setup(item => item.ListAsync(cancellationSource.Token))
            .Returns(() => ValueTask.FromCanceled<WorkspaceOperationResult<WorkspaceListOutcome>>(cancellationSource.Token));
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-list",
            cancellationToken: cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingTool_THEN_ShouldPropagateFailureWithoutCallingService()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceOpenTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-open",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement(42),
            },
            CancellationToken.None);

        await action.Should().ThrowAsync<Exception>();
        service.Verify(item => item.OpenAsync(
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_RequestContextWithoutArguments_WHEN_InvokingTool_THEN_ShouldUseEmptyArguments()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.ListAsync(CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult<WorkspaceListOutcome>.Succeeded(new WorkspaceListOutcome()));
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);
        var server = ServerOwnedToolTestSupport.CreateServer();
        await using var serverDisposal = server;
        var requestContext = new RequestContext<CallToolRequestParams>(
            server,
            new JsonRpcRequest
            {
                Method = "tools/call",
            },
            new CallToolRequestParams
            {
                Name = "workspace-list",
                Arguments = null,
            });

        var result = await target.InvokeAsync(requestContext, CancellationToken.None);

        result.IsError.Should().BeFalse();
        service.Verify(item => item.ListAsync(CancellationToken.None), Times.Once);
    }
}
