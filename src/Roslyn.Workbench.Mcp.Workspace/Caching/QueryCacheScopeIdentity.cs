namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCacheScopeIdentity
{
    public QueryCacheGeneration Generation { get; }

    public object Scope { get; }

    public QueryCacheScopeIdentity(QueryCacheGeneration generation, object scope)
    {
        Generation = generation;
        Scope = scope;
    }
}
