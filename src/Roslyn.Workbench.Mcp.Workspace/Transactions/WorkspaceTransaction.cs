using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceTransaction
{
    public required Solution BaselineSolution { get; init; }

    public IReadOnlyList<WorkspaceTransactionRevision> Revisions { get; init; } = [];

    public int CurrentRevision { get; init; }

    public int MaxRevisions { get; init; }

    public Solution CurrentSolution => CurrentRevision == 0
        ? BaselineSolution
        : Revisions[CurrentRevision - 1].Solution;

    public WorkspaceTransaction Append(WorkspaceTransactionRevision revision)
    {
        var revisions = Revisions
            .Take(CurrentRevision)
            .Append(revision)
            .ToArray();

        return this with
        {
            Revisions = revisions,
            CurrentRevision = revisions.Length,
        };
    }

    public WorkspaceTransaction? MoveHistory(TransactionHistoryDirection direction)
    {
        var revision = direction switch
        {
            TransactionHistoryDirection.Undo when CurrentRevision > 0 => CurrentRevision - 1,
            TransactionHistoryDirection.Redo when CurrentRevision < Revisions.Count => CurrentRevision + 1,
            _ => (int?)null,
        };

        return revision is null
            ? null
            : this with { CurrentRevision = revision.Value };
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
