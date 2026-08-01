namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceSnapshotLifecycleObserver
{
    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);

    void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
