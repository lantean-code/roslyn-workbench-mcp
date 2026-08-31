namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the state of the active transaction.
/// </summary>
public sealed record TransactionInfo
{
    /// <summary>
    /// Current staged transaction revision.
    /// </summary>
    [Description("Current staged transaction revision.")]
    public int Revision { get; init; }

    /// <summary>
    /// Number of revisions retained in transaction history.
    /// </summary>
    [Description("Number of revisions retained in transaction history.")]
    public int RevisionCount { get; init; }

    /// <summary>
    /// Maximum number of revisions the transaction can retain.
    /// </summary>
    [Description("Maximum number of revisions the transaction can retain.")]
    public int MaxRevisions { get; init; }

    /// <summary>
    /// Number of additional revisions that can be staged.
    /// </summary>
    [Description("Number of additional revisions that can be staged.")]
    public int RemainingRevisions { get; init; }

    /// <summary>
    /// Whether mutation tools may stage another change.
    /// </summary>
    [Description("Whether mutation tools may stage another change.")]
    public bool CanMutate { get; init; }

    /// <summary>
    /// Whether transaction history can move to the preceding revision.
    /// </summary>
    [Description("Whether transaction history can move to the preceding revision.")]
    public bool CanUndo { get; init; }

    /// <summary>
    /// Whether transaction history can move to the following revision.
    /// </summary>
    [Description("Whether transaction history can move to the following revision.")]
    public bool CanRedo { get; init; }

    /// <summary>
    /// Whether the staged transaction can be committed to disk.
    /// </summary>
    [Description("Whether the staged transaction can be committed to disk.")]
    public bool CanCommit { get; init; }

    /// <summary>
    /// Whether the complete staged transaction can be discarded.
    /// </summary>
    [Description("Whether the complete staged transaction can be discarded.")]
    public bool CanRollback { get; init; }
}
