using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionStartToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_StartingTransaction_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.StartAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<TransactionStartOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new TransactionStartOutcome
                {
                    Transaction = new TransactionInfo
                    {
                        Revision = 1,
                    },
                },
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionStartTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-start",
            ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
        service.Verify(item => item.StartAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            CancellationToken.None), Times.Once);
    }
}
