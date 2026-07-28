namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceSnapshotLifecycleObserver
{
    void InvalidateWorkspace(string workspaceId, long workspaceEpoch);

    void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
