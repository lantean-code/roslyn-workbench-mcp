namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the lifecycle state of the loaded workspace.
/// </summary>
public enum WorkspaceLifecycleState
{
    /// <summary>
    /// A workspace is loaded and ready.
    /// </summary>
    Ready,

    /// <summary>
    /// A transaction is active.
    /// </summary>
    TransactionActive,

    /// <summary>
    /// The active transaction is conflicted.
    /// </summary>
    TransactionConflicted,

    /// <summary>
    /// The workspace is out of date and requires reload.
    /// </summary>
    WorkspaceOutOfDate,
}
