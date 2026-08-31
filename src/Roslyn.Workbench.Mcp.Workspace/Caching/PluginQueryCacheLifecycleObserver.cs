namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Translates Workspace snapshot lifecycle events into plugin query-cache invalidations.
/// </summary>
internal sealed class PluginQueryCacheLifecycleObserver : IWorkspaceSnapshotLifecycleObserver
{
    private readonly IPluginQueryCacheState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginQueryCacheLifecycleObserver"/> class.
    /// </summary>
    /// <param name="state">The state whose generations are invalidated.</param>
    public PluginQueryCacheLifecycleObserver(IPluginQueryCacheState state)
    {
        _state = state;
    }

    /// <inheritdoc/>
    public void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch)
    {
        _state.InvalidateWorkspace(workspaceId, workspaceEpoch);
    }

    /// <inheritdoc/>
    public void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        _state.InvalidateTransaction(workspaceId, workspaceEpoch, transactionId);
    }

    /// <inheritdoc/>
    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        _state.InvalidateSnapshots(snapshots);
    }
}
