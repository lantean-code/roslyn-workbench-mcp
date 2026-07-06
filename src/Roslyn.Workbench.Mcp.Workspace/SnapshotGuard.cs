using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class SnapshotGuard : ISnapshotGuard
{
    private const string _transactionSnapshotMismatchCode = "SnapshotMismatch";

    public WorkspaceOperationError? Validate(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Transaction is null || expectedSnapshot is null)
        {
            return null;
        }

        if ((!string.IsNullOrWhiteSpace(expectedSnapshot.WorkspaceId) && !string.Equals(expectedSnapshot.WorkspaceId, session.Workspace.WorkspaceId, StringComparison.Ordinal))
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
