namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class PluginQueryCacheOptions
{
    public long EntryLimit { get; set; } = 10_000;

    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromHours(1);
}
