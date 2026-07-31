namespace Roslyn.Workbench.Mcp.Configuration;

internal sealed class StartupOptions
{
    public IReadOnlyList<string> PluginDirectories { get; set; } = [];

    public int DefaultMaxResults { get; set; } = 100;

    public TimeSpan CodeActionReferenceLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public long WorkspaceQueryCacheSizeLimit { get; set; } = 10_000;

    public long PluginQueryCacheEntryLimit { get; set; } = 10_000;

    public long CodeActionReferenceCacheSizeLimit { get; set; } = 75_000;

    public TimeSpan WorkspaceQueryCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan PluginQueryCacheSlidingExpiration { get; set; } = TimeSpan.FromHours(1);

    public int MaxTransactionRevisions { get; set; } = 20;

    public int MaxConcurrentQueries { get; set; } = 2;

    public ToolOutputSchemaMode ToolOutputSchemaMode { get; set; } = ToolOutputSchemaMode.Omit;

    public string StateDirectory { get; set; } = StateDirectoryDefaults.GetDefaultPath();

    public ErrorReportingOptions ErrorReporting { get; set; } = new();
}
