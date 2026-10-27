using System.Text.Json;

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
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionStartOutcome
            {
                Transaction = new TransactionInfo
                {
                    Revision = 1,
                },
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var boundRequest = new TransactionStartRequest
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
        var target = new TransactionStartTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-start",
            ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
        service.Verify(item => item.StartAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            CancellationToken.None), Times.Once);

        protocolFactory.Verify(item => item.CreateServerOwnedTool<TransactionStartRequest, TransactionStartData>(
            "transaction-start",
            "Transaction Start",
            "Starts an isolated staged transaction from the loaded workspace snapshot, returning transaction identity and revision zero. Use this before rename-symbol, format-document or mutating code actions, then pass the returned snapshot as expectedSnapshot to mutation requests. Only one workspace may own an active transaction in this Host; commit or roll back existing work before starting another. Reload an out-of-date workspace first. Check workspace-status and do not mutate a workspace that is or may be in use elsewhere unless mutation ownership has been coordinated. Starting a transaction does not write source files. Keep each transaction focused, inspect its preview or review, and finish promptly with transaction-commit or transaction-rollback.",
            false,
            false,
            null,
            ToolOutputSchemaMode.Omit), Times.Once);
    }
}
