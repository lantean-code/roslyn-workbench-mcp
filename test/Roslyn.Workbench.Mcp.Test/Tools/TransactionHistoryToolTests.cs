using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionHistoryToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_MovingHistory_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.MoveHistoryAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                TransactionHistoryDirection.Undo,
                It.IsAny<SnapshotPrecondition>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionHistoryOutcome
            {
                Transaction = new TransactionInfo
                {
                    Revision = 3,
                },
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionHistoryTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);
        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["direction"] = JsonSerializer.SerializeToElement(TransactionHistoryDirection.Undo);
        arguments["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition());

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-history",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(3);
        service.Verify(item => item.MoveHistoryAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            TransactionHistoryDirection.Undo,
            It.IsAny<SnapshotPrecondition>(),
            CancellationToken.None), Times.Once);
    }
}
