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

    public static WorkspaceSnapshotIdentity Create(
        WorkspaceIdentity workspace,
        WorkspaceSnapshotId committedSnapshotId,
        WorkspaceTransaction? transaction)
    {
        var snapshotId = committedSnapshotId;
        WorkspaceTransactionId? transactionId = null;
        if (transaction is not null)
        {
            snapshotId = transaction.CurrentSnapshotId;
            transactionId = transaction.TransactionId;
        }

        return new WorkspaceSnapshotIdentity(
            workspace.WorkspaceId,
            workspace.WorkspaceEpoch,
            snapshotId,
            transactionId);
    }
}
