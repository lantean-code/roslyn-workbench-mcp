namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned when a transaction is rolled back.
/// </summary>
internal sealed record TransactionRollbackData
{
    /// <summary>
    /// The resulting workspace state after rollback.
    /// </summary>
    [Description("The resulting workspace state after rollback.")]
    public TransactionRollbackState State { get; init; }
}
