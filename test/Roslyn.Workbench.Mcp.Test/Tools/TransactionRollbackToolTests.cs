using System.Text.Json;
using Microsoft.Extensions.Options;

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
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionRollbackOutcome
            {
                State = TransactionRollbackState.Ready,
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var boundRequest = new TransactionRollbackRequest
        {
            Workspace = includeWorkspace ? ServerOwnedToolTestData.CreateWorkspaceSelector() : null,
        };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);
        var target = new TransactionRollbackTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            service.Object);

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
