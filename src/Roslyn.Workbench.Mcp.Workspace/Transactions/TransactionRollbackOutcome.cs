using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record TransactionRollbackOutcome
{
    public TransactionRollbackState State { get; init; }
}
