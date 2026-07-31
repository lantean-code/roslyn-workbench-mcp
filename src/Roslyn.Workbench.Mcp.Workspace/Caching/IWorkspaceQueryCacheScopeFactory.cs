namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal interface IWorkspaceQueryCacheScopeFactory
{
    IWorkspaceQueryCacheScope CreateScope(
        string workspaceId,
        Solution solution,
        string componentIdentity);
}
