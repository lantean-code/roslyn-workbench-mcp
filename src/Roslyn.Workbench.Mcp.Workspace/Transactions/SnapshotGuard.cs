namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class SnapshotGuard : ISnapshotGuard
{
    private const string _transactionSnapshotMismatchCode = "SnapshotMismatch";

    public SnapshotValidationResult Validate(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot)
    {
        if (session.Transaction is null || expectedSnapshot is null)
        {
            return SnapshotValidationResult.Valid();
        }

        var currentSnapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            session.Transaction.CurrentRevision);

        if (expectedSnapshot != currentSnapshot)
        {
            var error = new WorkspaceOperationError
            {
                Code = _transactionSnapshotMismatchCode,
                Message = "The request snapshot does not match the current transaction snapshot.",
                RequiredAction = RequiredAction.ResolveTargetAgain,
            };

            return SnapshotValidationResult.Invalid(error);
        }

        return SnapshotValidationResult.Valid();
    }
}
