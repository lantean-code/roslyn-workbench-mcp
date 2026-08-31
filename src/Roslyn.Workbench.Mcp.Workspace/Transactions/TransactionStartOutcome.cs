namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes the newly active transaction.
/// </summary>
internal sealed record TransactionStartOutcome
{
    /// <summary>
    /// Gets the initial transaction state.
    /// </summary>
    public TransactionInfo Transaction { get; init; } = new();
}
