namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned when transaction history moves.
/// </summary>
internal sealed record TransactionHistoryData
{
    /// <summary>
    /// The active transaction info.
    /// </summary>
    [Description("The active transaction info.")]
    public TransactionInfo? Transaction { get; init; }
}
