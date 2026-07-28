namespace Roslyn.Workbench.Mcp.Workspace.State;

internal readonly record struct WorkspaceSnapshotIdentity
{
    public string WorkspaceId { get; }

    public long WorkspaceEpoch { get; }

    public WorkspaceSnapshotId SnapshotId { get; }

    public WorkspaceTransactionId? TransactionId { get; }

    public WorkspaceSnapshotIdentity(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceSnapshotId snapshotId,
        WorkspaceTransactionId? transactionId)
    {
        WorkspaceId = workspaceId;
        WorkspaceEpoch = workspaceEpoch;
        SnapshotId = snapshotId;
        TransactionId = transactionId;
    }
}
