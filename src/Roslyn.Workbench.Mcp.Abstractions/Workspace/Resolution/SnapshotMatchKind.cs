namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Represents the outcome of validating a snapshot precondition.
/// </summary>
public enum SnapshotMatchKind
{
    /// <summary>
    /// The snapshot precondition matched the current execution snapshot.
    /// </summary>
    Matched,

    /// <summary>
    /// The workspace epoch did not match the current execution snapshot.
    /// </summary>
    WorkspaceEpochMismatch,

    /// <summary>
    /// The opaque snapshot identifier did not match the current immutable solution.
    /// </summary>
    SnapshotIdMismatch,

    /// <summary>
    /// The transaction revision did not match the current execution snapshot.
    /// </summary>
    TransactionRevisionMismatch,
}
