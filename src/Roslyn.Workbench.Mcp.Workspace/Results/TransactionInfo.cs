namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the state of the active transaction.
/// </summary>
public sealed record TransactionInfo
{
    /// <summary>
    /// Gets the current transaction revision.
    /// </summary>
    [Description("Current staged transaction revision.")]
    public int Revision { get; init; }

    /// <summary>
    /// Gets the number of revisions currently stored.
    /// </summary>
    [Description("Number of revisions retained in transaction history.")]
    public int RevisionCount { get; init; }

    /// <summary>
    /// Gets the maximum number of revisions allowed.
    /// </summary>
    [Description("Maximum number of revisions the transaction can retain.")]
    public int MaxRevisions { get; init; }

    /// <summary>
    /// Gets the remaining revision capacity.
    /// </summary>
    [Description("Number of additional revisions that can be staged.")]
    public int RemainingRevisions { get; init; }

    /// <summary>
    /// Gets a value indicating whether mutation tools may stage changes.
    /// </summary>
    [Description("Whether mutation tools may stage another change.")]
    public bool CanMutate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can move backward.
    /// </summary>
    [Description("Whether transaction history can move to the preceding revision.")]
    public bool CanUndo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can move forward.
    /// </summary>
    [Description("Whether transaction history can move to the following revision.")]
    public bool CanRedo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can be committed.
    /// </summary>
    [Description("Whether the staged transaction can be committed to disk.")]
    public bool CanCommit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction can be rolled back.
    /// </summary>
    [Description("Whether the complete staged transaction can be discarded.")]
    public bool CanRollback { get; init; }
}
