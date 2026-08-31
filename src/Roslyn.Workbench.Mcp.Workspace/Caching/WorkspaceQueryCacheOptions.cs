namespace Roslyn.Workbench.Mcp.Workspace.Caching;

/// <summary>
/// Configures bounded retention for solution-scoped Workspace query values.
/// </summary>
internal sealed class WorkspaceQueryCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum aggregate charge retained by the cache.
    /// </summary>
    public long SizeLimit { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how long an entry may remain unused before eviction.
    /// </summary>
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromHours(1);
}
