using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to commit the active transaction.
/// </summary>
public sealed record TransactionCommitRequest
{
    /// <summary>
    /// Gets the expected snapshot precondition.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
