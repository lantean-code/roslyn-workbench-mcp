namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Projects a workspace session into the snapshot metadata returned with operation results.
/// </summary>
internal static class WorkspaceOperationContextFactory
{
    /// <summary>
    /// Captures the workspace and transaction snapshot visible to an operation.
    /// </summary>
    /// <param name="session">The workspace session in which the operation runs.</param>
    /// <returns>The caller-facing snapshot metadata for the session.</returns>
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
