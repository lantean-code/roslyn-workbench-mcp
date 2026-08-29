namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to move through transaction history.
/// </summary>
internal sealed record TransactionHistoryRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the requested history direction.
    /// </summary>
    [Description("Direction to move through transaction history: Undo or Redo.")]
    public required TransactionHistoryDirection Direction { get; init; }
}
