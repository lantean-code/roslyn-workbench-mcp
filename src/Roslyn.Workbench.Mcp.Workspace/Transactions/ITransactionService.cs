namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Executes the server-owned transaction lifecycle operations.
/// </summary>
internal interface ITransactionService
{
    /// <summary>
    /// Starts a new transaction.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken);

    /// <summary>
    /// Previews the active transaction.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="document">The optional document selector for a detailed diff.</param>
    /// <param name="includeDiff">A value indicating whether to include a detailed diff.</param>
    /// <param name="contextLines">The requested diff context line count.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    ValueTask<WorkspaceOperationResult<TransactionPreviewOutcome>> PreviewAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        DocumentSelector? document,
        bool includeDiff,
        int contextLines,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the active transaction backward or forward in history.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="direction">The requested history direction.</param>
    /// <param name="expectedSnapshot">The expected snapshot precondition.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    ValueTask<WorkspaceOperationResult<TransactionHistoryOutcome>> MoveHistoryAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        TransactionHistoryDirection direction,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="expectedSnapshot">The expected snapshot precondition.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        Guid? workspaceId,
        string? alias,
        string? path,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back the active transaction.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="alias">The optional workspace alias.</param>
    /// <param name="path">The optional workspace path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackAsync(Guid? workspaceId, string? alias, string? path, CancellationToken cancellationToken);
}
