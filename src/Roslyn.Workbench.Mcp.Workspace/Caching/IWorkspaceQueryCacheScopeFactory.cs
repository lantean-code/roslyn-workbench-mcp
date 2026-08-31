namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Creates component-isolated query-cache scopes for a specific Workspace solution snapshot.
/// </summary>
internal interface IWorkspaceQueryCacheScopeFactory
{
    /// <summary>
    /// Creates a cache scope for a component operating on a solution snapshot.
    /// </summary>
    /// <param name="workspaceId">The Workspace containing the solution.</param>
    /// <param name="solution">The immutable solution snapshot that distinguishes the scope generation.</param>
    /// <param name="componentIdentity">The stable identity of the component using the cache.</param>
    /// <returns>A cache scope bound to the Workspace, solution and component.</returns>
    IWorkspaceQueryCacheScope CreateScope(
        Guid workspaceId,
        Solution solution,
        string componentIdentity);
}
