namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the resulting workspace state after rollback.
/// </summary>
public enum TransactionRollbackState
{
    /// <summary>
    /// The workspace is ready after rollback.
    /// </summary>
    Ready,

    /// <summary>
    /// The workspace remains out of date after rollback.
    /// </summary>
    WorkspaceOutOfDate,
}
