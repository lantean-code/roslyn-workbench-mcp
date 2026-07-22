namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal interface IWorkspaceQueryCacheState
{
    void InvalidateWorkspace(string workspaceId);
}
