namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed record WorkspaceFailureContext
{
    public WorkspaceIdentity Workspace { get; }

    public WorkspaceLifecycleState LifecycleState { get; }

    public int ProjectCount { get; }

    public int DocumentCount { get; }

    public int? TransactionRevision { get; }

    public WorkspaceFailureContext(
        WorkspaceIdentity workspace,
        WorkspaceLifecycleState lifecycleState,
        int projectCount,
        int documentCount,
        int? transactionRevision)
    {
        Workspace = workspace;
        LifecycleState = lifecycleState;
        ProjectCount = projectCount;
        DocumentCount = documentCount;
        TransactionRevision = transactionRevision;
    }
}
