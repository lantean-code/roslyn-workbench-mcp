namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal static class WorkspaceOperationContextFactory
{
    public static WorkspaceOperationContext Create(WorkspaceSessionSnapshot session)
    {
        var transactionRevision = session.Transaction?.CurrentRevision;
        var snapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            transactionRevision);

        var context = new WorkspaceOperationContext
        {
            Snapshot = snapshot,
        };

        return context;
    }
}
