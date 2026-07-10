namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

/// <summary>
/// Represents the expected workspace snapshot for a location- or transaction-based request.
/// </summary>
public sealed record SnapshotPrecondition
{
    /// <summary>
    /// Gets the workspace identifier associated with the expected snapshot.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the expected workspace epoch.
    /// </summary>
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the expected transaction revision, when available.
    /// </summary>
    public int? TransactionRevision { get; init; }
}
