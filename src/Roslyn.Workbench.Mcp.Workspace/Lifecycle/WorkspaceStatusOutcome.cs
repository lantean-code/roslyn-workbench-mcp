namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Describes a loaded workspace's current lifecycle, transaction, input, and cross-instance state.
/// </summary>
internal sealed record WorkspaceStatusOutcome
{
    /// <summary>
    /// Gets the workspace lifecycle state.
    /// </summary>
    public WorkspaceLifecycleState State { get; init; }

    /// <summary>
    /// Gets the workspace identity.
    /// </summary>
    public required WorkspaceIdentity Workspace { get; init; }

    /// <summary>
    /// Gets the number of supported projects in the current solution.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the number of documents across the supported projects.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets load and instance diagnostics when requested or when instance coordination requires attention.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo>? LoadDiagnostics { get; init; }

    /// <summary>
    /// Gets the active transaction state, when a transaction exists.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workspace must be reloaded before further operations.
    /// </summary>
    public bool ReloadRequired { get; init; }

    /// <summary>
    /// Gets the detected external input change, when one has been observed.
    /// </summary>
    public WorkspaceInputChange? ExternalChange { get; init; }

    /// <summary>
    /// Gets other live server instances known to have the same workspace open.
    /// </summary>
    public IReadOnlyList<WorkspaceInstanceInfo> Instances { get; init; } = [];
}
