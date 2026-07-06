namespace Roslyn.Workbench.Mcp.Plugins.Workspace;

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
    /// The transaction revision did not match the current execution snapshot.
    /// </summary>
    TransactionRevisionMismatch,
}
