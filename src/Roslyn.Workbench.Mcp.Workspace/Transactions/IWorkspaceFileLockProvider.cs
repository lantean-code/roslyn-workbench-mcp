namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Acquires an exclusive workspace commit lock backed by a lock file.
/// </summary>
internal interface IWorkspaceFileLockProvider
{
    /// <summary>
    /// Attempts to acquire exclusive ownership of a lock file.
    /// </summary>
    /// <param name="lockPath">The path of the workspace ownership lock to acquire.</param>
    /// <returns>The owned lock, or <see langword="null"/> when the lock is unavailable.</returns>
    IWorkspaceCommitLock? TryAcquire(string lockPath);
}
