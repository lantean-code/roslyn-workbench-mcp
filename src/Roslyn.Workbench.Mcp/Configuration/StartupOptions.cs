namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Configures plugin discovery, query limits, transaction retention and host-owned services.
/// </summary>
internal sealed class StartupOptions
{
    /// <summary>
    /// Gets or sets the directories searched for plugin packages during startup.
    /// </summary>
    public IReadOnlyList<string> PluginDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets the default item limit applied when a query request omits its own limit.
    /// </summary>
    public int DefaultMaxResults { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long a discovered Code Action reference remains replayable.
    /// </summary>
    public TimeSpan CodeActionReferenceLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum aggregate charge retained by the workspace query cache.
    /// </summary>
    public long WorkspaceQueryCacheSizeLimit { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of entries retained across plugin query-cache partitions.
    /// </summary>
    public long PluginQueryCacheEntryLimit { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum aggregate charge retained by the Code Action reference cache.
    /// </summary>
    public long CodeActionReferenceCacheSizeLimit { get; set; } = 75_000;

    /// <summary>
    /// Gets or sets how long an unused workspace query-cache entry remains valid.
    /// </summary>
    public TimeSpan WorkspaceQueryCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets how long an unused plugin query-cache entry remains valid.
    /// </summary>
    public TimeSpan PluginQueryCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of staged revisions retained by one transaction.
    /// </summary>
    public int MaxTransactionRevisions { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of query operations allowed to execute concurrently.
    /// </summary>
    public int MaxConcurrentQueries { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether output schemas are published with MCP tool metadata.
    /// </summary>
    public ToolOutputSchemaMode ToolOutputSchemaMode { get; set; } = ToolOutputSchemaMode.Omit;

    /// <summary>
    /// Gets or sets the directory used for durable coordination and recovery state.
    /// </summary>
    public string StateDirectory { get; set; } = StateDirectoryDefaults.GetDefaultPath();

    /// <summary>
    /// Gets or sets error-report consent, retention and payload limits.
    /// </summary>
    public ErrorReportingOptions ErrorReporting { get; set; } = new();
}
