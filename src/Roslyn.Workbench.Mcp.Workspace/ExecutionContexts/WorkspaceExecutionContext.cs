namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContext : IWorkspaceExecutionContext
{
    public WorkspaceExecutionContext(
        Solution currentSolution,
        WorkspaceIdentity workspaceIdentity,
        int? transactionRevision,
        int defaultMaxResults,
        IWorkspaceResolver workspaceResolver)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        TransactionRevision = transactionRevision;
        DefaultMaxResults = defaultMaxResults;
        WorkspaceResolver = workspaceResolver;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }
}
