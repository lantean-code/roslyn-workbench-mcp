namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents the structured payload returned when a transaction is rolled back.
/// </summary>
internal sealed record TransactionRollbackData
{
    /// <summary>
    /// Gets the resulting workspace state after rollback.
    /// </summary>
    public TransactionRollbackState State { get; init; }
}
