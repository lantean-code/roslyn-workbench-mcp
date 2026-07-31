namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class PluginQueryCacheLifecycleObserver : IWorkspaceSnapshotLifecycleObserver
{
    private readonly IPluginQueryCacheState _state;

    public PluginQueryCacheLifecycleObserver(IPluginQueryCacheState state)
    {
        _state = state;
    }

    public void InvalidateWorkspace(string workspaceId, long workspaceEpoch)
    {
        _state.InvalidateWorkspace(workspaceId, workspaceEpoch);
    }

    public void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        _state.InvalidateTransaction(workspaceId, workspaceEpoch, transactionId);
    }

    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        _state.InvalidateSnapshots(snapshots);
    }
}
