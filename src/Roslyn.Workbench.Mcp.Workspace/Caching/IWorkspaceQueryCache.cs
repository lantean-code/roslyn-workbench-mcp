namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal interface IWorkspaceQueryCache
{
    void InvalidateWorkspace(Guid workspaceId);
}
