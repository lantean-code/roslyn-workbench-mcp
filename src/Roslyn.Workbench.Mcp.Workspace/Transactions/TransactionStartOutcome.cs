using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record TransactionStartOutcome
{
    public TransactionInfo Transaction { get; init; } = new();
}
