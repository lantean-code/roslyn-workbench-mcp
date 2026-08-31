namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Exposes Workspace lifecycle invalidation without revealing the cache-state implementation.
/// </summary>
internal sealed class WorkspaceQueryCache : IWorkspaceQueryCache
{
    private readonly IWorkspaceQueryCacheState _workspaceState;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceQueryCache"/> class.
    /// </summary>
    /// <param name="workspaceState">The state that owns Workspace cache generations.</param>
    public WorkspaceQueryCache(IWorkspaceQueryCacheState workspaceState)
    {
        _workspaceState = workspaceState;
    }

    /// <inheritdoc/>
    public void InvalidateWorkspace(Guid workspaceId)
    {
        _workspaceState.InvalidateWorkspace(workspaceId);
    }
}
