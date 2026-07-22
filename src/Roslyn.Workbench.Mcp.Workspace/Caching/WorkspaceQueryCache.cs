namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class WorkspaceQueryCache : IWorkspaceQueryCache
{
    private readonly IWorkspaceQueryCacheState _workspaceState;

    public WorkspaceQueryCache(IWorkspaceQueryCacheState workspaceState)
    {
        _workspaceState = workspaceState;
    }

    public void InvalidateWorkspace(string workspaceId)
    {
        _workspaceState.InvalidateWorkspace(workspaceId);
    }
}
