namespace Roslyn.Workbench.Mcp.Workspace.Configuration;

/// <summary>
/// Represents configuration shared by workspace subsystems.
/// </summary>
internal sealed class WorkspaceOptions
{
    /// <summary>
    /// Gets or sets whether supported source mutation is enabled by Host policy.
    /// </summary>
    public bool SourceMutationEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether commit requires authorisation bound to a transaction review receipt.
    /// </summary>
    public bool ReceiptAuthorisationRequired { get; set; }

    /// <summary>
    /// Gets or sets whether commit requires successful no-new-compiler-error validation.
    /// </summary>
    public bool CompilerValidationRequired { get; set; }

    /// <summary>
    /// Gets or sets how generated-looking checked-in source is handled by mutation workflows.
    /// </summary>
    public GeneratedSourcePolicy GeneratedSourcePolicy { get; set; } = GeneratedSourcePolicy.Warn;

    /// <summary>
    /// Gets or sets Workspace-relative wildcard patterns exempted from generated-source classification.
    /// </summary>
    public IReadOnlyList<string> GeneratedSourceExceptions { get; set; } = [];

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
