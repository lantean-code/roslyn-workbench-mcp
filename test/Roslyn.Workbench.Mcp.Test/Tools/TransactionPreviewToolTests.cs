using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

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
            .ReturnsAsync(new WorkspaceOperationResult<TransactionPreviewOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new TransactionPreviewOutcome
                {
                    Transaction = new TransactionInfo
                    {
                        Revision = 2,
                    },
                    Diff = new DocumentDiff
                    {
                        Truncated = false,
                    },
                },
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionPreviewTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);
        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["includeDiff"] = JsonSerializer.SerializeToElement(true);
        arguments["contextLines"] = JsonSerializer.SerializeToElement(2);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-preview",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        result.StructuredContent.Value.GetProperty("diff").GetProperty("truncated").GetBoolean().Should().BeFalse();
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
