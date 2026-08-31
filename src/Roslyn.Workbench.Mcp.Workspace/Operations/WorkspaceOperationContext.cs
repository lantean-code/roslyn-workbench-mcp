namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Identifies the workspace snapshot against which an operation executed.
/// </summary>
internal sealed class WorkspaceOperationContext
{
    /// <summary>
    /// Gets the caller-facing snapshot precondition for the operation.
    /// </summary>
    public SnapshotPrecondition? Snapshot { get; init; }

    /// <summary>
    /// Gets the identifier of the workspace in which the operation ran.
    /// </summary>
    public Guid? WorkspaceId => Snapshot?.WorkspaceId;

    /// <summary>
    /// Gets the load epoch in which the operation ran.
    /// </summary>
    public long? WorkspaceEpoch => Snapshot?.WorkspaceEpoch;

    /// <summary>
    /// Gets the transaction revision against which the operation ran, when applicable.
    /// </summary>
    public int? TransactionRevision => Snapshot?.TransactionRevision;
}
