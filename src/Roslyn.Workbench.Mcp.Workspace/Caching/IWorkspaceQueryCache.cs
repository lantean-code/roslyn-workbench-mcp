namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Exposes lifecycle invalidation for the Workspace-scoped query cache.
/// </summary>
internal interface IWorkspaceQueryCache
{
    /// <summary>
    /// Invalidates all query-cache entries associated with a Workspace.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier to invalidate.</param>
    void InvalidateWorkspace(Guid workspaceId);
}
