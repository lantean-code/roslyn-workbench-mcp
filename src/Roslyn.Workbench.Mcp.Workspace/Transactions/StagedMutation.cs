namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Carries the immutable session and transaction state produced by appending a mutation revision.
/// </summary>
internal sealed record StagedMutation
{
    /// <summary>
    /// Gets the replacement workspace session containing the staged revision.
    /// </summary>
    public required WorkspaceSessionSnapshot Session { get; init; }

    /// <summary>
    /// Gets transaction state after staging.
    /// </summary>
    public required WorkspaceTransaction Transaction { get; init; }

    /// <summary>
    /// Gets the newly appended transaction revision.
    /// </summary>
    public required WorkspaceTransactionRevision Revision { get; init; }

    /// <summary>
    /// Gets the changed-document identities in the new revision.
    /// </summary>
    public required ChangeSummary Changes { get; init; }

    /// <summary>
    /// Gets snapshot identifiers made unreachable when redo history was discarded.
    /// </summary>
    public IReadOnlyList<WorkspaceSnapshotId> DiscardedSnapshotIds { get; init; } = [];
}
