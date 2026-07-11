using System.Text.Json;

using Microsoft.Extensions.Options;
using ModelContextProtocol;

using Roslyn.Workbench.Mcp.Test.ToolExecution;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class ServerOwnedToolBaseTests
{
    [Fact]
    public void GIVEN_ReadOnlyTool_WHEN_InspectingProtocolMetadata_THEN_ShouldPublishExpectedAnnotations()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        var target = new WorkspaceListTool(
            Options.Create(new StartupOptions()),
            service.Object);

        target.ProtocolTool.Name.Should().Be("workspace-list");
        target.ProtocolTool.Title.Should().Be("Workspace List");
        target.ProtocolTool.Annotations!.ReadOnlyHint.Should().BeTrue();
        target.ProtocolTool.Annotations.IdempotentHint.Should().BeTrue();
        target.ProtocolTool.Annotations.DestructiveHint.Should().BeFalse();
        target.ProtocolTool.Annotations.OpenWorldHint.Should().BeFalse();
        target.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_DestructiveTool_WHEN_InspectingProtocolMetadata_THEN_ShouldPublishExpectedAnnotations()
    {
        var service = new Mock<ITransactionService>();
        var target = new TransactionCommitTool(
            Options.Create(new StartupOptions()),
            service.Object);

        target.ProtocolTool.Annotations!.ReadOnlyHint.Should().BeFalse();
        target.ProtocolTool.Annotations.IdempotentHint.Should().BeFalse();
        target.ProtocolTool.Annotations.DestructiveHint.Should().BeTrue();
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Conflict")]
    [InlineData("Faulted")]
    public async Task GIVEN_WorkspaceFailure_WHEN_InvokingTool_THEN_ShouldPublishStructuredFailure(
        string statusName)
    {
        var status = Enum.Parse<WorkspaceOperationStatus>(statusName);
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.ListAsync(CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceListOutcome>
            {
                Status = status,
                Error = new WorkspaceOperationError
                {
                    Code = statusName,
                    Message = "Message",
                    RequiredAction = RequiredAction.Retry,
                },
            });
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), service.Object);

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
    public async Task GIVEN_ServiceThrows_WHEN_InvokingTool_THEN_ShouldPublishUnhandledFailure()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.ListAsync(CancellationToken.None))
            .Returns(ValueTask.FromException<WorkspaceOperationResult<WorkspaceListOutcome>>(new InvalidOperationException("Message")));
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-list",
            cancellationToken: CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
    }

    [Fact]
    public async Task GIVEN_ServiceCancellation_WHEN_InvokingTool_THEN_ShouldPropagateCancellation()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        service
            .Setup(item => item.ListAsync(cancellationSource.Token))
            .Returns(ValueTask.FromCanceled<WorkspaceOperationResult<WorkspaceListOutcome>>(cancellationSource.Token));
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), service.Object);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-list",
            cancellationToken: cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_MalformedArguments_WHEN_InvokingTool_THEN_ShouldPublishUnhandledFailureWithoutCallingService()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        var target = new WorkspaceOpenTool(Options.Create(new StartupOptions()), service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-open",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement(42),
            },
            CancellationToken.None);

        McpServerToolResultAssertions.AssertUnhandledFailure(result);
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
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceListOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceListOutcome(),
            });
        var target = new WorkspaceListTool(Options.Create(new StartupOptions()), service.Object);
        var requestContext = new RequestContext<CallToolRequestParams>(
            ServerOwnedToolTestSupport.CreateServer(),
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
