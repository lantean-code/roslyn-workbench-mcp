using System.Text.Json;

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
                It.IsAny<SnapshotPrecondition>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionCommitOutcome
            {
                Committed = true,
                Transaction = new TransactionInfo
                {
                    Revision = 4,
                },
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var expectedSnapshot = new SnapshotPrecondition
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };
        var boundRequest = new TransactionCommitRequest
        {
            Workspace = includeWorkspace ? ServerOwnedToolTestData.CreateWorkspaceSelector() : null,
            ExpectedSnapshot = expectedSnapshot,
        };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);
        var target = new TransactionCommitTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            service.Object);

        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition { WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111") });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-commit",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("committed").GetBoolean().Should().BeTrue();
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(4);
        service.Verify(item => item.CommitAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            It.IsAny<SnapshotPrecondition>(),
            CancellationToken.None), Times.Once);
    }
}
