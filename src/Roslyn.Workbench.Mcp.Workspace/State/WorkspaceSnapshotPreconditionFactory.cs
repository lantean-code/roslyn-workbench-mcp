namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Projects an internal Workspace snapshot identity into the public optimistic-concurrency precondition.
/// </summary>
internal static class WorkspaceSnapshotPreconditionFactory
{
    /// <summary>
    /// Creates a snapshot precondition for a current committed or transactional revision.
    /// </summary>
    /// <param name="snapshotIdentity">The fully qualified internal snapshot identity.</param>
    /// <param name="transactionRevision">The active transaction revision when applicable.</param>
    /// <returns>The public snapshot precondition.</returns>
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
