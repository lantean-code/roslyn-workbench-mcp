namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to move through transaction history.
/// </summary>
internal sealed record TransactionHistoryRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Direction to move through transaction history: Undo or Redo.
    /// </summary>
    [Description("Direction to move through transaction history: Undo or Redo.")]
    public required TransactionHistoryDirection Direction { get; init; }
}
