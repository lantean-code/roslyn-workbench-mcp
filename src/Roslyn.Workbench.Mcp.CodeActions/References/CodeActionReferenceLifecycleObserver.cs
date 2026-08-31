namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Removes Code Action references when their owning workspace state becomes invalid.
/// </summary>
internal sealed class CodeActionReferenceLifecycleObserver : IWorkspaceSnapshotLifecycleObserver
{
    private readonly ICodeActionReferenceState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionReferenceLifecycleObserver"/> class.
    /// </summary>
    /// <param name="state">The reference state to invalidate.</param>
    public CodeActionReferenceLifecycleObserver(ICodeActionReferenceState state)
    {
        _state = state;
    }

    /// <summary>
    /// Invalidates references owned by the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch that the operation expects.</param>
    public void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch)
    {
        _state.InvalidateWorkspace(workspaceId, workspaceEpoch);
    }

    /// <summary>
    /// Invalidates references owned by the specified transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch that the operation expects.</param>
    /// <param name="transactionId">The transaction identifier.</param>
    public void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        _state.InvalidateTransaction(workspaceId, workspaceEpoch, transactionId);
    }

    /// <summary>
    /// Invalidates references whose workspace snapshots are stale.
    /// </summary>
    /// <param name="snapshots">The snapshot identities whose Code Action references must be invalidated.</param>
    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        _state.InvalidateSnapshots(snapshots);
    }
}
