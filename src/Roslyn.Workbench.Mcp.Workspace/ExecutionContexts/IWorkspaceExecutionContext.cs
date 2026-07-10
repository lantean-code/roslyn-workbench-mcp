namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal interface IWorkspaceExecutionContext
{
    Solution CurrentSolution { get; }

    WorkspaceIdentity WorkspaceIdentity { get; }

    int? TransactionRevision { get; }

    int DefaultMaxResults { get; }

    IWorkspaceResolver WorkspaceResolver { get; }
}
