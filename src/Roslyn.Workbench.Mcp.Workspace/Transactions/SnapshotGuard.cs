namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class SnapshotGuard : ISnapshotGuard
{
    private const string _transactionSnapshotMismatchCode = "SnapshotMismatch";

    public WorkspaceOperationError? Validate(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot)
    {
        if (session.Transaction is null || expectedSnapshot is null)
        {
            return null;
        }

        if (expectedSnapshot.WorkspaceId != session.Workspace.WorkspaceId
            || expectedSnapshot.WorkspaceEpoch != session.Workspace.WorkspaceEpoch
            || expectedSnapshot.TransactionRevision != session.Transaction.CurrentRevision)
        {
            return new WorkspaceOperationError
            {
                Code = _transactionSnapshotMismatchCode,
                Message = "The request snapshot does not match the current transaction snapshot.",
                RequiredAction = RequiredAction.ResolveTargetAgain,
            };
        }

        return null;
    }
}
