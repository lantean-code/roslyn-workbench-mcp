namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

internal interface ICodeActionExecutionContext
{
    Solution CurrentSolution { get; }

    WorkspaceIdentity WorkspaceIdentity { get; }

    WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    SnapshotPrecondition Snapshot { get; }

    int? TransactionRevision { get; }

    int DefaultMaxResults { get; }

    IWorkspacePathService WorkspacePathService { get; }

    IWorkspaceResolver WorkspaceResolver { get; }
}
