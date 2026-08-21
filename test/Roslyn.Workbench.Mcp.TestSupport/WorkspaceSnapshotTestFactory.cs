using Roslyn.Workbench.Mcp.Workspace.Results;

namespace Roslyn.Workbench.Mcp.TestSupport;

internal static class WorkspaceSnapshotTestFactory
{
    public static WorkspaceSnapshotId CreateId(long value)
    {
        return new WorkspaceSnapshotId(CreateGuid(value));
    }

    public static SnapshotPrecondition CreatePrecondition(
        Guid workspaceId,
        long workspaceEpoch = 1,
        long snapshotId = 1,
        int? transactionRevision = null)
    {
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            SnapshotId = CreateGuid(snapshotId),
            TransactionRevision = transactionRevision,
        };

        return snapshot;
    }

    public static SnapshotPrecondition CreatePrecondition(
        WorkspaceSnapshotIdentity snapshotIdentity,
        int? transactionRevision)
    {
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceId = snapshotIdentity.WorkspaceId,
            WorkspaceEpoch = snapshotIdentity.WorkspaceEpoch,
            SnapshotId = snapshotIdentity.SnapshotId.Value,
            TransactionRevision = transactionRevision,
        };

        return snapshot;
    }

    public static Guid CreateGuid(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return Guid.Parse($"00000000-0000-0000-0000-{value:X12}");
    }

    public static WorkspaceOperationContext CreateContext(
        Guid workspaceId,
        long workspaceEpoch = 1,
        long snapshotId = 1,
        int? transactionRevision = null)
    {
        var snapshot = CreatePrecondition(
            workspaceId,
            workspaceEpoch,
            snapshotId,
            transactionRevision);

        var context = new WorkspaceOperationContext
        {
            Snapshot = snapshot,
        };

        return context;
    }

    public static WorkspaceOperationContext CreateContext(
        WorkspaceSnapshotIdentity snapshotIdentity,
        int? transactionRevision)
    {
        var snapshot = CreatePrecondition(snapshotIdentity, transactionRevision);

        var context = new WorkspaceOperationContext
        {
            Snapshot = snapshot,
        };

        return context;
    }

    public static WorkspaceFailureContext CreateFailureContext(
        Guid workspaceId,
        long workspaceEpoch = 1,
        int? transactionRevision = null,
        WorkspaceLifecycleState lifecycleState = WorkspaceLifecycleState.Ready,
        int projectCount = 2,
        int documentCount = 3)
    {
        var workspace = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
        };

        var context = new WorkspaceFailureContext(
            workspace,
            lifecycleState,
            projectCount,
            documentCount,
            transactionRevision);

        return context;
    }
}
