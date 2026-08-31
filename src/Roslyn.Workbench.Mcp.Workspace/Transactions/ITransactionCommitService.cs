namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Persists the active transaction through the durable commit and recovery pipeline.
/// </summary>
internal interface ITransactionCommitService
{
    /// <summary>
    /// Commits the selected transaction to the workspace files.
    /// </summary>
    /// <param name="selection">The resolved workspace selection on which the operation runs.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace operation result.</returns>
    ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken);
}
