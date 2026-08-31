namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes transaction state after moving through revision history.
/// </summary>
internal sealed record TransactionHistoryOutcome
{
    /// <summary>
    /// Gets transaction state at the selected revision.
    /// </summary>
    public TransactionInfo Transaction { get; init; } = new();
}
