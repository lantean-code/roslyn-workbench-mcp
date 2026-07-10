namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

/// <summary>
/// Represents the state of the active transaction.
/// </summary>
public sealed record TransactionInfo
{
    /// <summary>
    /// Gets the current transaction revision.
    /// </summary>
    public int Revision { get; init; }

    /// <summary>
    /// Gets the number of revisions currently stored.
    /// </summary>
    public int RevisionCount { get; init; }

    /// <summary>
    /// Gets the maximum number of revisions allowed.
    /// </summary>
    public int MaxRevisions { get; init; }

    /// <summary>
    /// Gets the remaining revision capacity.
    /// </summary>
    public int RemainingRevisions { get; init; }

    /// <summary>
    /// Gets a value indicating whether mutation tools may stage changes.
    /// </summary>
    public bool CanMutate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can move backward.
    /// </summary>
    public bool CanUndo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can move forward.
    /// </summary>
    public bool CanRedo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can be committed.
    /// </summary>
    public bool CanCommit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can be rolled back.
    /// </summary>
    public bool CanRollback { get; init; }
}
