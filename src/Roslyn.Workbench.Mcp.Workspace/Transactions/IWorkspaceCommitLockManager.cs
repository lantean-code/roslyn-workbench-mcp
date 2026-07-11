namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceCommitLockManager
{
    WorkspaceCommitLockAcquisition Acquire(string workspaceRoot);
}
