namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes workspace state after the active transaction is discarded.
/// </summary>
internal sealed record TransactionRollbackOutcome
{
    /// <summary>
    /// Gets the lifecycle state reached after rollback.
    /// </summary>
    public TransactionRollbackState State { get; init; }
}
