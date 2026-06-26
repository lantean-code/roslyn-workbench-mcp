using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to move through transaction history.
/// </summary>
public sealed record TransactionHistoryRequest
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
