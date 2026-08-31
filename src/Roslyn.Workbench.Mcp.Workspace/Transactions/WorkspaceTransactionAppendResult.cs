namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Carries an updated transaction and any forward-history snapshots discarded by an append.
/// </summary>
internal sealed record WorkspaceTransactionAppendResult
{
    /// <summary>
    /// Gets the transaction containing the appended revision.
    /// </summary>
    public required WorkspaceTransaction Transaction { get; init; }

    /// <summary>
    /// Gets snapshot identifiers made unreachable when forward history was discarded.
    /// </summary>
    public IReadOnlyList<WorkspaceSnapshotId> DiscardedSnapshotIds { get; init; } = [];
}
