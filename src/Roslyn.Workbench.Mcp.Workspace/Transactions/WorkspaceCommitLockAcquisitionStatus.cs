namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal enum WorkspaceCommitLockAcquisitionStatus
{
    Acquired,
    Contended,
    Failed,
}
