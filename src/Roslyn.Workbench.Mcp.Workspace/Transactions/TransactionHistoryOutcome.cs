using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record TransactionHistoryOutcome
{
    public TransactionInfo Transaction { get; init; } = new();
}
