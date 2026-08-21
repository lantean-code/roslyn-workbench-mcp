namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContext : IWorkspaceExecutionContext
{
    public WorkspaceExecutionContext(
        Solution currentSolution,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceSnapshotIdentity snapshotIdentity,
        SnapshotPrecondition snapshot,
        int? transactionRevision,
        int defaultMaxResults,
        IWorkspacePathService workspacePathService,
        IWorkspaceResolver workspaceResolver)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        SnapshotIdentity = snapshotIdentity;
        Snapshot = snapshot;
        TransactionRevision = transactionRevision;
        DefaultMaxResults = defaultMaxResults;
        WorkspacePathService = workspacePathService;
        WorkspaceResolver = workspaceResolver;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    public SnapshotPrecondition Snapshot { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspacePathService WorkspacePathService { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }
}
