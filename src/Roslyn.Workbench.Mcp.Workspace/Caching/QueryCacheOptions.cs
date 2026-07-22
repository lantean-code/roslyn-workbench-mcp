namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCacheOptions
{
    public long SizeLimit { get; set; } = 50_000;

    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(10);
}
