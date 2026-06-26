namespace Roslyn.Workbench.Mcp.Workspace;

internal enum WorkspaceTrigger
{
    OpenSucceeded,
    CloseSucceeded,
    ExternalChangeDetected,
    ReloadSucceeded,
    TransactionStarted,
    TransactionCommitted,
    TransactionRolledBack,
    TransactionConflictDetected,
    ConflictedRollbackCompleted,
}
