namespace Roslyn.Workbench.Mcp.Workspace;

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
