using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionPreviewToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_PreviewingTransaction_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.PreviewAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                null,
                true,
                2,
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionPreviewOutcome
            {
                Transaction = new TransactionInfo
                {
                    Revision = 2,
                },
                Diff = new DocumentDiff
                {
                    Truncated = false,
                },
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var boundRequest = new TransactionPreviewRequest
        {
            Workspace = includeWorkspace ? ServerOwnedToolTestData.CreateWorkspaceSelector() : null,
            IncludeDiff = true,
            ContextLines = 2,
        };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);
        var target = new TransactionPreviewTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            service.Object);
        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["includeDiff"] = JsonSerializer.SerializeToElement(true);
        arguments["contextLines"] = JsonSerializer.SerializeToElement(2);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-preview",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        data.GetProperty("diff").GetProperty("truncated").GetBoolean().Should().BeFalse();
        service.Verify(item => item.PreviewAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            null,
            true,
            2,
            CancellationToken.None), Times.Once);
    }
}
