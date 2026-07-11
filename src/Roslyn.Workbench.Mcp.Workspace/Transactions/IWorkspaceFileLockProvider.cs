namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceFileLockProvider
{
    IWorkspaceCommitLock? TryAcquire(string lockPath);
}
