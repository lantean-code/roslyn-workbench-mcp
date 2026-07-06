namespace Roslyn.Workbench.Mcp.Workspace.State;

internal enum WorkspaceTrigger
{
    ExternalChangeDetected,
    ReloadSucceeded,
    TransactionStarted,
    TransactionCommitted,
    TransactionRolledBack,
    TransactionConflictDetected,
    ConflictedRollbackCompleted,
}
