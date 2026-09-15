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
    OpenWorkspace = 0,

    /// <summary>
    /// Start a transaction before retrying the request.
    /// </summary>
    StartTransaction = 1,

    /// <summary>
    /// Roll back the current transaction before retrying the request.
    /// </summary>
    RollbackTransaction = 2,

    /// <summary>
    /// Reload the workspace before retrying the request.
    /// </summary>
    ReloadWorkspace = 3,

    /// <summary>
    /// Resolve the target again against the current snapshot.
    /// </summary>
    ResolveTargetAgain = 4,

    /// <summary>
    /// Commit or roll back the current transaction before continuing.
    /// </summary>
    CommitOrRollback = 5,

    /// <summary>
    /// Reduce transaction history before continuing.
    /// </summary>
    ReduceTransactionHistory = 6,

    /// <summary>
    /// Retry the request later.
    /// </summary>
    Retry = 7,

    /// <summary>
    /// Resolve unfinished recovery work before continuing.
    /// </summary>
    ResolveRecovery = 8,

    /// <summary>
    /// Narrow the request before retrying it.
    /// </summary>
    NarrowRequest = 9,

    /// <summary>
    /// Review the current transaction and obtain a new exact-change receipt.
    /// </summary>
    ReviewTransaction = 10,
}
