namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Represents the configuration options for the workspace coordinator.
/// </summary>
internal sealed record WorkspaceCoordinatorOptions
{
    /// <summary>
    /// Gets the maximum number of concurrent query leases.
    /// </summary>
    public int MaxConcurrentQueries { get; init; } = 2;

    /// <summary>
    /// Gets the effective result limit for query execution.
    /// </summary>
    public int DefaultMaxResults { get; init; } = 100;

    /// <summary>
    /// Gets the maximum serialized response size, in bytes, for query execution.
    /// </summary>
    public int MaxResponseBytes { get; init; } = 4 * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of stored transaction revisions.
    /// </summary>
    public int MaxTransactionRevisions { get; init; } = 20;

    /// <summary>
    /// Gets the maximum number of workspaces that may be loaded at once.
    /// </summary>
    public int MaxLoadedWorkspaces { get; init; } = 4;

    /// <summary>
    /// Gets the state directory used for recovery records.
    /// </summary>
    public string StateDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state");
}
