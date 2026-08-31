namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Classifies an attempt to acquire cross-process workspace commit ownership.
/// </summary>
internal enum WorkspaceCommitLockAcquisitionStatus
{
    /// <summary>
    /// The caller acquired and owns the commit lock.
    /// </summary>
    Acquired,
    /// <summary>
    /// Another process currently owns the commit lock.
    /// </summary>
    Contended,
    /// <summary>
    /// Lock acquisition failed for a reason other than contention.
    /// </summary>
    Failed,
}
