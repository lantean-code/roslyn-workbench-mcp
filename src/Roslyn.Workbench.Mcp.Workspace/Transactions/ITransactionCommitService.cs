namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface ITransactionCommitService
{
    ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken);
}
