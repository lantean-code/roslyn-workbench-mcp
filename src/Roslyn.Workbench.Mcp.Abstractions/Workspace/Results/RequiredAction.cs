namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the semantic follow-up required after a failed Workspace operation.
/// The Host translates this value into its published continuation contract.
/// </summary>
public enum RequiredAction
{
    /// <summary>
    /// Open a workspace before retrying the request.
    /// </summary>
    OpenWorkspace,

    /// <summary>
    /// Start a transaction before retrying the request.
    /// </summary>
    StartTransaction,

    /// <summary>
    /// Roll back the current transaction before retrying the request.
    /// </summary>
    RollbackTransaction,

    /// <summary>
    /// Reload the workspace before retrying the request.
    /// </summary>
    ReloadWorkspace,

    /// <summary>
    /// Resolve the target again against the current snapshot.
    /// </summary>
    ResolveTargetAgain,

    /// <summary>
    /// Commit or roll back the current transaction before continuing.
    /// </summary>
    CommitOrRollback,

    /// <summary>
    /// Reduce transaction history before continuing.
    /// </summary>
    ReduceTransactionHistory,

    /// <summary>
    /// Retry the request later.
    /// </summary>
    Retry,

    /// <summary>
    /// Resolve unfinished recovery work before continuing.
    /// </summary>
    ResolveRecovery,

    /// <summary>
    /// Narrow the request before retrying it.
    /// </summary>
    NarrowRequest,
}
