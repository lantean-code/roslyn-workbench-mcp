namespace Roslyn.Workbench.Mcp.Workspace.State;

internal static class WorkspaceSnapshotPreconditionFactory
{
    public static SnapshotPrecondition Create(WorkspaceSnapshotIdentity snapshotIdentity, int? transactionRevision)
    {
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceId = snapshotIdentity.WorkspaceId,
            WorkspaceEpoch = snapshotIdentity.WorkspaceEpoch,
            SnapshotId = snapshotIdentity.SnapshotId.Value,
            TransactionRevision = transactionRevision,
        };

        return snapshot;
    }
}
