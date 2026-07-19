namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to move through transaction history.
/// </summary>
internal sealed record TransactionHistoryRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the requested history direction.
    /// </summary>
    public TransactionHistoryDirection Direction { get; init; }

    /// <summary>
    /// Gets the expected snapshot precondition.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
