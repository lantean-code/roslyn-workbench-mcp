using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record TransactionCommitOutcome
{
    public bool Committed { get; init; }

    public TransactionInfo? Transaction { get; init; }
}
