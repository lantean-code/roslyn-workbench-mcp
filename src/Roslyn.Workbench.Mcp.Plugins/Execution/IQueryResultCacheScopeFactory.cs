namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal interface IQueryResultCacheScopeFactory
{
    QueryResultCacheScope CreateScope(
        WorkspaceSnapshotIdentity snapshotIdentity,
        string pluginId,
        string toolName);
}
