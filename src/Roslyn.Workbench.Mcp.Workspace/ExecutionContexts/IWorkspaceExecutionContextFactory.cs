namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal interface IWorkspaceExecutionContextFactory
{
    WorkspaceExecutionContextLease CreateQueryContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken);

    WorkspaceMutationExecutionLease CreateMutationContext(
        WorkspaceSelector? workspace,
        CancellationToken cancellationToken);
}
