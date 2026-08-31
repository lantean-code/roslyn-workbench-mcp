namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Maintains the baseline and bounded revision history of one staged workspace transaction.
/// </summary>
internal sealed record WorkspaceTransaction
{
    /// <summary>
    /// Gets the process-unique transaction identifier.
    /// </summary>
    public required WorkspaceTransactionId TransactionId { get; init; }

    /// <summary>
    /// Gets the committed snapshot from which the transaction started.
    /// </summary>
    public required WorkspaceSnapshotId BaselineSnapshotId { get; init; }

    /// <summary>
    /// Gets the immutable solution from which the transaction started.
    /// </summary>
    public required Solution BaselineSolution { get; init; }

    /// <summary>
    /// Gets staged revisions in history order.
    /// </summary>
    public IReadOnlyList<WorkspaceTransactionRevision> Revisions { get; init; } = [];

    /// <summary>
    /// Gets the zero-based current position in revision history.
    /// </summary>
    public int CurrentRevision { get; init; }

    /// <summary>
    /// Gets the maximum number of staged revisions the transaction may retain.
    /// </summary>
    public int MaxRevisions { get; init; }

    /// <summary>
    /// Gets the solution at the current history position.
    /// </summary>
    public Solution CurrentSolution => CurrentRevision == 0
        ? BaselineSolution
        : Revisions[CurrentRevision - 1].Solution;

    /// <summary>
    /// Gets the snapshot identifier at the current history position.
    /// </summary>
    public WorkspaceSnapshotId CurrentSnapshotId => CurrentRevision == 0
        ? BaselineSnapshotId
        : Revisions[CurrentRevision - 1].SnapshotId;

    /// <summary>
    /// Appends a revision at the current history position and discards any forward revisions.
    /// </summary>
    /// <param name="revision">The transaction revision.</param>
    /// <returns>The updated transaction and the snapshot identifiers discarded from forward history.</returns>
    public WorkspaceTransactionAppendResult Append(WorkspaceTransactionRevision revision)
    {
        var discardedSnapshotIds = Revisions
            .Skip(CurrentRevision)
            .Select(static item => item.SnapshotId)
            .ToArray();

        var revisions = Revisions
            .Take(CurrentRevision)
            .Append(revision)
            .ToArray();

        var transaction = this with
        {
            Revisions = revisions,
            CurrentRevision = revisions.Length,
        };

        return new WorkspaceTransactionAppendResult
        {
            Transaction = transaction,
            DiscardedSnapshotIds = discardedSnapshotIds,
        };
    }

    /// <summary>
    /// Moves the transaction backward or forward through revision history.
    /// </summary>
    /// <param name="direction">The direction in which to move through transaction history.</param>
    /// <returns>The transaction at the requested history position.</returns>
    public WorkspaceTransaction? MoveHistory(TransactionHistoryDirection direction)
    {
        var revision = direction switch
        {
            TransactionHistoryDirection.Undo when CurrentRevision > 0 => CurrentRevision - 1,
            TransactionHistoryDirection.Redo when CurrentRevision < Revisions.Count => CurrentRevision + 1,
            _ => (int?)null,
        };

        if (revision is null)
        {
            return null;
        }

        return this with { CurrentRevision = revision.Value };
    }

    /// <summary>
    /// Creates the externally reported transaction information.
    /// </summary>
    /// <param name="conflicted">Whether the transaction is currently in a conflicted state.</param>
    /// <returns>The caller-facing transaction state and available operations.</returns>
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
