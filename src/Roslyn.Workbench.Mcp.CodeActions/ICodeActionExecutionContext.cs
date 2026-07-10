namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionExecutionContext
{
    Solution CurrentSolution { get; }

    WorkspaceIdentity WorkspaceIdentity { get; }

    int? TransactionRevision { get; }

    int DefaultMaxResults { get; }

    IWorkspaceResolver WorkspaceResolver { get; }
}
