namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Fully qualifies a solution snapshot by Workspace, load epoch, snapshot and optional transaction.
/// </summary>
internal readonly record struct WorkspaceSnapshotIdentity
{
    /// <summary>
    /// Gets the stable Workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the Workspace load epoch.
    /// </summary>
    public long WorkspaceEpoch { get; }

    /// <summary>
    /// Gets the current committed or transactional snapshot identifier.
    /// </summary>
    public WorkspaceSnapshotId SnapshotId { get; }

    /// <summary>
    /// Gets the transaction identifier when the snapshot is transactional.
    /// </summary>
    public WorkspaceTransactionId? TransactionId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSnapshotIdentity"/> structure.
    /// </summary>
    /// <param name="workspaceId">The stable Workspace identifier.</param>
    /// <param name="workspaceEpoch">The Workspace load epoch.</param>
    /// <param name="snapshotId">The committed or transactional snapshot identifier.</param>
    /// <param name="transactionId">The optional transaction identifier.</param>
    public WorkspaceSnapshotIdentity(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceSnapshotId snapshotId,
        WorkspaceTransactionId? transactionId)
    {
        WorkspaceId = workspaceId;
        WorkspaceEpoch = workspaceEpoch;
        SnapshotId = snapshotId;
        TransactionId = transactionId;
    }

    /// <summary>
    /// Creates the currently visible identity from Workspace, committed snapshot and transaction state.
    /// </summary>
    /// <param name="workspace">The Workspace identity and load epoch.</param>
    /// <param name="committedSnapshotId">The committed snapshot identifier.</param>
    /// <param name="transaction">The active transaction, when present.</param>
    /// <returns>The current committed or transactional snapshot identity.</returns>
    public static WorkspaceSnapshotIdentity Create(
        WorkspaceIdentity workspace,
        WorkspaceSnapshotId committedSnapshotId,
        WorkspaceTransaction? transaction)
    {
        var snapshotId = committedSnapshotId;
        WorkspaceTransactionId? transactionId = null;
        if (transaction is not null)
        {
            snapshotId = transaction.CurrentSnapshotId;
            transactionId = transaction.TransactionId;
        }

        return new WorkspaceSnapshotIdentity(
            workspace.WorkspaceId,
            workspace.WorkspaceEpoch,
            snapshotId,
            transactionId);
    }
}
