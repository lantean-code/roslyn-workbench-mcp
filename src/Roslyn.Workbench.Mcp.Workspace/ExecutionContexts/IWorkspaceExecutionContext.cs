namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal interface IWorkspaceExecutionContext
{
    Solution CurrentSolution { get; }

    WorkspaceIdentity WorkspaceIdentity { get; }

    WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    int? TransactionRevision { get; }

    int DefaultMaxResults { get; }

    IWorkspacePathService WorkspacePathService { get; }

    IWorkspaceResolver WorkspaceResolver { get; }
}
