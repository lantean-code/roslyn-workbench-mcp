using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned when transaction history moves.
/// </summary>
public sealed record TransactionHistoryData
{
    /// <summary>
    /// Gets the active transaction info.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }
}
