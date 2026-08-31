namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Defines events that drive the Workspace lifecycle state machine.
/// </summary>
internal enum WorkspaceTrigger
{
    /// <summary>
    /// A monitored Workspace input changed.
    /// </summary>
    ExternalChangeDetected,

    /// <summary>
    /// A stale Workspace reloaded successfully.
    /// </summary>
    ReloadSucceeded,

    /// <summary>
    /// A transaction acquired ownership and began.
    /// </summary>
    TransactionStarted,

    /// <summary>
    /// The active transaction committed successfully.
    /// </summary>
    TransactionCommitted,

    /// <summary>
    /// The active transaction rolled back without a conflict.
    /// </summary>
    TransactionRolledBack,

    /// <summary>
    /// An external change conflicted with the active transaction.
    /// </summary>
    TransactionConflictDetected,

    /// <summary>
    /// A conflicted transaction finished rollback and left the Workspace stale.
    /// </summary>
    ConflictedRollbackCompleted,
}
