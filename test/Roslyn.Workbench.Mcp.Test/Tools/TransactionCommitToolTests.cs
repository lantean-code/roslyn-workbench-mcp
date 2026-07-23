using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionCommitToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_CommittingTransaction_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.CommitAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                null,
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult<TransactionCommitOutcome>.Succeeded(new TransactionCommitOutcome
            {
                Committed = true,
                Transaction = new TransactionInfo
                {
                    Revision = 4,
                },
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionCommitTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-commit",
            ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("committed").GetBoolean().Should().BeTrue();
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(4);
        service.Verify(item => item.CommitAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            null,
            CancellationToken.None), Times.Once);
    }
}
