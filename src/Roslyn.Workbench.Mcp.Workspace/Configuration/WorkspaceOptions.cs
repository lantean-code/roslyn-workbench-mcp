namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Represents configuration shared by workspace subsystems.
/// </summary>
internal sealed class WorkspaceOptions
{
    /// <summary>
    /// Gets the maximum number of concurrent query leases.
    /// </summary>
    public int MaxConcurrentQueries { get; set; } = 2;

    /// <summary>
    /// Gets the effective result limit for query execution.
    /// </summary>
    public int DefaultMaxResults { get; set; } = 100;

    /// <summary>
    /// Gets the maximum number of stored transaction revisions.
    /// </summary>
    public int MaxTransactionRevisions { get; set; } = 20;

    /// <summary>
    /// Gets the maximum number of workspaces that may be loaded at once.
    /// </summary>
    public int MaxLoadedWorkspaces { get; set; } = 4;

    /// <summary>
    /// Gets the state directory used for recovery records.
    /// </summary>
    public string StateDirectory { get; set; } = StateDirectoryDefaults.GetDefaultPath();
}
