namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCacheGeneration
{
    public object Partition { get; }

    public CancellationToken InvalidationToken { get; }

    public QueryCacheGeneration(object partition, CancellationToken invalidationToken)
    {
        Partition = partition;
        InvalidationToken = invalidationToken;
    }
}
