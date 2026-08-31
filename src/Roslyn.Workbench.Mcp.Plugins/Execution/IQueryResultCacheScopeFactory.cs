namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Creates query-result cache scopes isolated by snapshot, plugin and tool identity.
/// </summary>
internal interface IQueryResultCacheScopeFactory
{
    /// <summary>
    /// Creates a cache scope for one plugin tool invocation boundary.
    /// </summary>
    /// <param name="snapshotIdentity">The immutable workspace snapshot identity.</param>
    /// <param name="pluginId">The owning plugin identity.</param>
    /// <param name="toolName">The registered tool name.</param>
    /// <returns>A cache scope that releases its retained entries when disposed.</returns>
    QueryResultCacheScope CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName);
}
