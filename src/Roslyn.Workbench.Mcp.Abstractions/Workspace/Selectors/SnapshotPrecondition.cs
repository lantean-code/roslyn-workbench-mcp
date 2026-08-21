namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents the expected workspace snapshot for a location- or transaction-based request.
/// </summary>
public sealed record SnapshotPrecondition
{
    /// <summary>
    /// Gets the workspace identifier associated with the expected snapshot.
    /// </summary>
    [NonEmptyGuid]
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the expected workspace epoch.
    /// </summary>
    public required long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the expected immutable solution snapshot.
    /// </summary>
    [NonEmptyGuid]
    public required Guid SnapshotId { get; init; }

    /// <summary>
    /// Gets the expected transaction revision, when available.
    /// </summary>
    public required int? TransactionRevision { get; init; }
}
