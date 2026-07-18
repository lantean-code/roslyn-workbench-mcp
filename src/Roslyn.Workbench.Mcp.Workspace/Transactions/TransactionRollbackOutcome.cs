namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record TransactionRollbackOutcome
{
    public TransactionRollbackState State { get; init; }
}
