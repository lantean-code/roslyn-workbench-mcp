using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace;

internal interface ITransactionCommitService
{
    ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitAsync(
        WorkspaceSelection selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken);
}
