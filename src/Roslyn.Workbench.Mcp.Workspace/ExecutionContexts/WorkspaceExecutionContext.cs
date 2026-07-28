namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContext : IWorkspaceExecutionContext
{
    public WorkspaceExecutionContext(
        Solution currentSolution,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceSnapshotIdentity snapshotIdentity,
        int? transactionRevision,
        int defaultMaxResults,
        IWorkspaceResolver workspaceResolver)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        SnapshotIdentity = snapshotIdentity;
        TransactionRevision = transactionRevision;
        DefaultMaxResults = defaultMaxResults;
        WorkspaceResolver = workspaceResolver;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }
}
