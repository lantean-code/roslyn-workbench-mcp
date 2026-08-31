namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Observes snapshot retirement so caches and other derived state can invalidate matching generations.
/// </summary>
internal interface IWorkspaceSnapshotLifecycleObserver
{
    /// <summary>
    /// Invalidates every snapshot associated with a loaded Workspace epoch.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier.</param>
    /// <param name="workspaceEpoch">The load epoch being retired.</param>
    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);

    /// <summary>
    /// Invalidates snapshots associated with one transaction.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier.</param>
    /// <param name="workspaceEpoch">The load epoch containing the transaction.</param>
    /// <param name="transactionId">The transaction being retired.</param>
    void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    /// <summary>
    /// Invalidates the specified snapshot identities.
    /// </summary>
    /// <param name="snapshots">The retired snapshots.</param>
    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
