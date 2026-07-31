namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class WorkspaceQueryCacheOptions
{
    public long SizeLimit { get; set; } = 10_000;

    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromHours(1);
}
