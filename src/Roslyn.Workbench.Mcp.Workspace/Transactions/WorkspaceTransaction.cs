using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceTransaction
{
    public Solution BaselineSolution { get; init; } = null!;

    public IReadOnlyList<WorkspaceTransactionRevision> Revisions { get; init; } = [];

    public int CurrentRevision { get; init; }

    public int MaxRevisions { get; init; }

    public Solution CurrentSolution
    {
        get
        {
            return CurrentRevision == 0
                ? BaselineSolution
                : Revisions[CurrentRevision - 1].Solution;
        }
    }

    public TransactionInfo ToInfo(bool conflicted)
    {
        return new TransactionInfo
        {
            Revision = CurrentRevision,
            RevisionCount = Revisions.Count,
            MaxRevisions = MaxRevisions,
            RemainingRevisions = Math.Max(0, MaxRevisions - CurrentRevision),
            CanMutate = !conflicted && CurrentRevision < MaxRevisions,
            CanUndo = CurrentRevision > 0,
            CanRedo = CurrentRevision < Revisions.Count,
            CanCommit = !conflicted && CurrentRevision > 0,
            CanRollback = true,
        };
    }
}
