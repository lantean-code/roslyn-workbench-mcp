namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Enforces optimistic-concurrency preconditions for transaction operations.
/// </summary>
internal sealed class SnapshotGuard : ISnapshotGuard
{
    private const string _transactionSnapshotMismatchCode = "SnapshotMismatch";

    /// <summary>
    /// Verifies that a request snapshot still identifies the current transaction revision.
    /// </summary>
    /// <param name="session">The workspace session in which the operation runs.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <returns>A valid result when the snapshot is current or no check is required; otherwise, a snapshot-mismatch error.</returns>
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
