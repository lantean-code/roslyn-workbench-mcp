using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

public interface IWorkspaceRuntime : IToolExecutionContextFactory
{
    ValueTask<ToolResult<WorkspaceOpenData>> OpenAsync(WorkspaceOpenRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<WorkspaceListData>> ListAsync(WorkspaceListRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<WorkspaceCloseData>> CloseAsync(WorkspaceCloseRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<WorkspaceStatusData>> GetStatusAsync(WorkspaceStatusRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<WorkspaceReloadData>> ReloadAsync(WorkspaceReloadRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<TransactionStartData>> StartTransactionAsync(TransactionStartRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<TransactionPreviewData>> PreviewTransactionAsync(TransactionPreviewRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<TransactionHistoryData>> MoveTransactionHistoryAsync(TransactionHistoryRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<TransactionCommitData>> CommitTransactionAsync(TransactionCommitRequest request, CancellationToken cancellationToken);

    ValueTask<ToolResult<TransactionRollbackData>> RollbackTransactionAsync(TransactionRollbackRequest request, CancellationToken cancellationToken);
}
