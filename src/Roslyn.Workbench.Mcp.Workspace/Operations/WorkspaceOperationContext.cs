namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed class WorkspaceOperationContext
{
    public SnapshotPrecondition? Snapshot { get; init; }

    public Guid? WorkspaceId => Snapshot?.WorkspaceId;

    public long? WorkspaceEpoch => Snapshot?.WorkspaceEpoch;

    public int? TransactionRevision => Snapshot?.TransactionRevision;
}
