namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned when a transaction starts.
/// </summary>
internal sealed record TransactionStartData
{
    /// <summary>
    /// The active transaction info.
    /// </summary>
    [Description("The active transaction info.")]
    public TransactionInfo? Transaction { get; init; }
}
