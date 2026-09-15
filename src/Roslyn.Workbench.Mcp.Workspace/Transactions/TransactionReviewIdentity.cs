namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Binds a canonical persistence digest to one exact Workspace transaction state.
/// </summary>
internal sealed record TransactionReviewIdentity
{
    /// <summary>
    /// Gets the canonicalisation algorithm identifier.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the lowercase hexadecimal SHA-256 digest of the canonical persistence set.
    /// </summary>
    public required string ChangeSetDigest { get; init; }

    /// <summary>
    /// Gets the stable Workspace identifier.
    /// </summary>
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the Workspace load epoch.
    /// </summary>
    public required long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the process-local transaction identifier.
    /// </summary>
    public required long TransactionId { get; init; }

    /// <summary>
    /// Gets the immutable solution snapshot identifier.
    /// </summary>
    public required Guid SnapshotId { get; init; }

    /// <summary>
    /// Gets the reviewed transaction revision.
    /// </summary>
    public required int TransactionRevision { get; init; }
}
