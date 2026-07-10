using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;
using Roslyn.Workbench.Mcp.Workspace.Operations;
using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class TransactionToolUnitTests
{
    [Fact]
    public async Task GIVEN_StartRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedTransactionStartResult()
    {
        var transactionService = new Mock<ITransactionService>();
        var request = new TransactionStartRequest
        {
            Workspace = CreateWorkspaceSelector(),
        };
        var target = new TransactionStartTool(Options.Create(new StartupOptions()), transactionService.Object);

        transactionService
            .Setup(service => service.StartAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", CancellationToken.None))
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

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-start",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_PreviewRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedTransactionPreviewResult()
    {
        var transactionService = new Mock<ITransactionService>();
        var request = new TransactionPreviewRequest
        {
            Workspace = CreateWorkspaceSelector(),
            IncludeDiff = true,
            ContextLines = 2,
        };
        var target = new TransactionPreviewTool(Options.Create(new StartupOptions()), transactionService.Object);

        transactionService
            .Setup(service => service.PreviewAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", null, true, 2, CancellationToken.None))
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

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-preview",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
                ["includeDiff"] = JsonSerializer.SerializeToElement(request.IncludeDiff),
                ["contextLines"] = JsonSerializer.SerializeToElement(request.ContextLines),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(2);
        result.StructuredContent.Value.GetProperty("diff").GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_HistoryRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedTransactionHistoryResult()
    {
        var transactionService = new Mock<ITransactionService>();
        var request = new TransactionHistoryRequest
        {
            Workspace = CreateWorkspaceSelector(),
            Direction = TransactionHistoryDirection.Undo,
        };
        var target = new TransactionHistoryTool(Options.Create(new StartupOptions()), transactionService.Object);

        transactionService
            .Setup(service => service.MoveHistoryAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", TransactionHistoryDirection.Undo, null, CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<TransactionHistoryOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new TransactionHistoryOutcome
                {
                    Transaction = new TransactionInfo
                    {
                        Revision = 3,
                    },
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-history",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
                ["direction"] = JsonSerializer.SerializeToElement(request.Direction),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GIVEN_RollbackRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedTransactionRollbackResult()
    {
        var transactionService = new Mock<ITransactionService>();
        var request = new TransactionRollbackRequest
        {
            Workspace = CreateWorkspaceSelector(),
        };
        var target = new TransactionRollbackTool(Options.Create(new StartupOptions()), transactionService.Object);

        transactionService
            .Setup(service => service.RollbackAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<TransactionRollbackOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new TransactionRollbackOutcome
                {
                    State = TransactionRollbackState.Ready,
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-rollback",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("state").GetString().Should().Be("Ready");
    }

    [Fact]
    public async Task GIVEN_CommitRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedTransactionCommitResult()
    {
        var transactionService = new Mock<ITransactionService>();
        var request = new TransactionCommitRequest
        {
            Workspace = CreateWorkspaceSelector(),
        };
        var target = new TransactionCommitTool(Options.Create(new StartupOptions()), transactionService.Object);

        transactionService
            .Setup(service => service.CommitAsync("WorkspaceId", "Alias", "/workspace/Sample.csproj", null, CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<TransactionCommitOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new TransactionCommitOutcome
                {
                    Committed = true,
                    Transaction = new TransactionInfo
                    {
                        Revision = 4,
                    },
                },
            });

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-commit",
            new Dictionary<string, JsonElement>
            {
                ["workspace"] = JsonSerializer.SerializeToElement(request.Workspace),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("committed").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(4);
    }

    private static WorkspaceSelector CreateWorkspaceSelector()
    {
        return new WorkspaceSelector
        {
            WorkspaceId = "WorkspaceId",
            Alias = "Alias",
            Path = "/workspace/Sample.csproj",
        };
    }
}
