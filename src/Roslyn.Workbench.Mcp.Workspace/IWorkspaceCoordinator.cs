using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

/// <summary>
/// Coordinates the loaded Roslyn workspace and its lifecycle tools.
/// </summary>
public interface IWorkspaceCoordinator : IToolExecutionContextFactory
{
    /// <summary>
    /// Opens the requested workspace.
    /// </summary>
    /// <param name="request">The open request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The open result.</returns>
    ValueTask<ToolResult<WorkspaceOpenData>> OpenAsync(WorkspaceOpenRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the currently loaded workspaces.
    /// </summary>
    /// <param name="request">The list request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list result.</returns>
    ValueTask<ToolResult<WorkspaceListData>> ListAsync(WorkspaceListRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the selected workspace.
    /// </summary>
    /// <param name="request">The close request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The close result.</returns>
    ValueTask<ToolResult<WorkspaceCloseData>> CloseAsync(WorkspaceCloseRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the selected workspace status.
    /// </summary>
    /// <param name="request">The status request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workspace status.</returns>
    ValueTask<ToolResult<WorkspaceStatusData>> GetStatusAsync(WorkspaceStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reloads the selected workspace.
    /// </summary>
    /// <param name="request">The reload request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reload result.</returns>
    ValueTask<ToolResult<WorkspaceReloadData>> ReloadAsync(WorkspaceReloadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a new transaction.
    /// </summary>
    /// <param name="request">The transaction start request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction start result.</returns>
    ValueTask<ToolResult<TransactionStartData>> StartTransactionAsync(TransactionStartRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Previews the active transaction.
    /// </summary>
    /// <param name="request">The transaction preview request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction preview result.</returns>
    ValueTask<ToolResult<TransactionPreviewData>> PreviewTransactionAsync(TransactionPreviewRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the active transaction backward or forward in history.
    /// </summary>
    /// <param name="request">The transaction history request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction history result.</returns>
    ValueTask<ToolResult<TransactionHistoryData>> MoveTransactionHistoryAsync(TransactionHistoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    /// <param name="request">The transaction commit request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction commit result.</returns>
    ValueTask<ToolResult<TransactionCommitData>> CommitTransactionAsync(TransactionCommitRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back the active transaction.
    /// </summary>
    /// <param name="request">The transaction rollback request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction rollback result.</returns>
    ValueTask<ToolResult<TransactionRollbackData>> RollbackTransactionAsync(TransactionRollbackRequest request, CancellationToken cancellationToken);
}
