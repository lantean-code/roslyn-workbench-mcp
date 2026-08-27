namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the direction of transaction history movement.
/// </summary>
public enum TransactionHistoryDirection
{
    /// <summary>
    /// Move to the prior transaction revision.
    /// </summary>
    Undo,

    /// <summary>
    /// Move to the next transaction revision.
    /// </summary>
    Redo,
}
