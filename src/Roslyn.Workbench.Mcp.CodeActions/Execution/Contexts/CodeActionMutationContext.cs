namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

internal sealed class CodeActionMutationContext : ICodeActionMutationContext
{
    public CodeActionMutationContext(IWorkspaceExecutionContext workspaceContext)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        SnapshotIdentity = workspaceContext.SnapshotIdentity;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

}
