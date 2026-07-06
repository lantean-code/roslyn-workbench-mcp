using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Transactions;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record TransactionRollbackOutcome
{
    public TransactionRollbackState State { get; init; }
}
