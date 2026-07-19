namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents the structured payload returned when transaction history moves.
/// </summary>
internal sealed record TransactionHistoryData
{
    /// <summary>
    /// Gets the active transaction info.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }
}
