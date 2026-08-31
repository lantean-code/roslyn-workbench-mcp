namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Records one immutable staged solution and its mutation metadata in transaction history.
/// </summary>
internal sealed record WorkspaceTransactionRevision
{
    /// <summary>
    /// Gets the immutable snapshot identifier assigned to the revision.
    /// </summary>
    public required WorkspaceSnapshotId SnapshotId { get; init; }

    /// <summary>
    /// Gets the staged solution at this revision.
    /// </summary>
    public required Solution Solution { get; init; }

    /// <summary>
    /// Gets the changed-document identities introduced by the revision.
    /// </summary>
    public required ChangeSummary Changes { get; init; }

    /// <summary>
    /// Gets the mutation operation name recorded in history.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the concise summary of the staged mutation.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the compact preview produced when the revision was staged.
    /// </summary>
    public required MutationPreview Preview { get; init; }
}
