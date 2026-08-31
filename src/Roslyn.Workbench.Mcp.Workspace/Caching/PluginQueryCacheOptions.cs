namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Configures bounded retention for values cached by plugin query handlers.
/// </summary>
internal sealed class PluginQueryCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retained plugin query entries.
    /// </summary>
    public long EntryLimit { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how long an entry may remain unused before eviction.
    /// </summary>
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromHours(1);
}
