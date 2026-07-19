namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents the structured payload returned when a transaction is committed.
/// </summary>
internal sealed record TransactionCommitData
{
    /// <summary>
    /// Gets a value indicating whether the commit succeeded.
    /// </summary>
    public bool Committed { get; init; }

    /// <summary>
    /// Gets the final transaction info, when available.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }
}
