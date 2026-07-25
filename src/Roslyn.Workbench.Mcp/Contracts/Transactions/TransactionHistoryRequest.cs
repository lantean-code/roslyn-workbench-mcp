namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to move through transaction history.
/// </summary>
internal sealed record TransactionHistoryRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the requested history direction.
    /// </summary>
    public TransactionHistoryDirection Direction { get; init; }
}
