namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Represents the expected workspace snapshot for a location- or transaction-based request.
/// </summary>
public sealed record SnapshotPrecondition
{
    /// <summary>
    /// Gets the workspace identifier associated with the expected snapshot.
    /// </summary>
    [Description("The workspace identifier associated with the expected snapshot.")]
    [NonEmptyGuid]
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the expected workspace epoch.
    /// </summary>
    [Description("The expected workspace epoch.")]
    public required long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the expected immutable solution snapshot.
    /// </summary>
    [Description("The opaque identifier of the expected immutable solution snapshot.")]
    [NonEmptyGuid]
    public required Guid SnapshotId { get; init; }

    /// <summary>
    /// Gets the expected transaction revision, when available.
    /// </summary>
    [Description("The expected transaction revision, when available.")]
    public required int? TransactionRevision { get; init; }
}
