namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Acquires cross-process commit ownership for a workspace root.
/// </summary>
internal interface IWorkspaceCommitLockManager
{
    /// <summary>
    /// Acquires the workspace commit lock.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <returns>The acquired lock or a classified contention or failure result.</returns>
    WorkspaceCommitLockAcquisition Acquire(string workspaceRoot);
}
