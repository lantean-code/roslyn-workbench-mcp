namespace Roslyn.Workbench.Mcp.Workspace;

internal interface IWorkspaceSessionStore
{
    WorkspaceHostSnapshot ReadSnapshot();

    WorkspaceSessionSnapshot? ReadSession(string workspaceId);

    string AllocateWorkspaceId();

    long AllocateWorkspaceEpoch();

    WorkspaceOperationError? TryAddWorkspace(WorkspaceSessionSnapshot session, Func<WorkspaceHostSnapshot, WorkspaceOperationError?> validate);

    WorkspaceSessionSnapshot? RemoveWorkspace(string workspaceId);

    void ReplaceSession(WorkspaceSessionSnapshot session);

    void ReplaceSessionAndSetTransactionOwner(WorkspaceSessionSnapshot session, string? transactionOwnerWorkspaceId);
}
