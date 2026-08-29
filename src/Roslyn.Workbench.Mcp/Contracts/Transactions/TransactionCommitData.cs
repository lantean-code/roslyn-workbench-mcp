namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned when a transaction is committed.
/// </summary>
internal sealed record TransactionCommitData
{
    /// <summary>
    /// Gets a value indicating whether the commit succeeded.
    /// </summary>
    [Description("Whether the commit succeeded.")]
    public bool Committed { get; init; }

    /// <summary>
    /// Gets the final transaction info, when available.
    /// </summary>
    [Description("The final transaction info, when available.")]
    public TransactionInfo? Transaction { get; init; }
}
