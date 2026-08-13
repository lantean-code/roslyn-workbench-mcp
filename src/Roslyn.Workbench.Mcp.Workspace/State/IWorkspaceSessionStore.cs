namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceSessionStore
{
    WorkspaceHostSnapshot ReadSnapshot();

    WorkspaceSessionSnapshot? ReadSession(Guid workspaceId);

    Guid AllocateWorkspaceId();

    long AllocateWorkspaceEpoch();

    WorkspaceSnapshotId AllocateWorkspaceSnapshotId();

    WorkspaceTransactionId AllocateWorkspaceTransactionId();

    WorkspaceOperationError? TryAddWorkspace(WorkspaceSessionSnapshot session, Func<WorkspaceHostSnapshot, WorkspaceOperationError?> validate);

    WorkspaceSessionSnapshot? RemoveWorkspace(Guid workspaceId);

    IReadOnlyList<WorkspaceSessionSnapshot> DrainWorkspaces();

    void ReplaceSession(WorkspaceSessionSnapshot session);

    void ReplaceSessionAfterStaging(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<WorkspaceSnapshotId> discardedSnapshotIds);

    void ReplaceSessionAndSetTransactionOwner(WorkspaceSessionSnapshot session, Guid? transactionOwnerWorkspaceId);
}
