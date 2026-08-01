namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal interface IWorkspaceQueryCacheScopeFactory
{
    IWorkspaceQueryCacheScope CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity);
}
