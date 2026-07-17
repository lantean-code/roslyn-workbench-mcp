using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionRollbackToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_RollingBackTransaction_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.RollbackAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<TransactionRollbackOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new TransactionRollbackOutcome
                {
                    State = TransactionRollbackState.Ready,
                },
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionRollbackTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-rollback",
            ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("state").GetString().Should().Be("Ready");
        service.Verify(item => item.RollbackAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            CancellationToken.None), Times.Once);
    }
}
