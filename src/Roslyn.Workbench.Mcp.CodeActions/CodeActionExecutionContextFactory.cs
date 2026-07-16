namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionExecutionContextFactory : ICodeActionExecutionContextFactory
{
    private readonly IWorkspaceExecutionContextFactory _workspaceFactory;

    public CodeActionExecutionContextFactory(IWorkspaceExecutionContextFactory workspaceFactory)
    {
        _workspaceFactory = workspaceFactory;
    }

    public CodeActionQueryExecutionLease CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateQueryContext(request.Workspace, cancellationToken);
        var context = workspaceLease.Context is null
            ? null
            : new CodeActionQueryContext(workspaceLease.Context);
        return new CodeActionQueryExecutionLease(
            workspaceLease,
            context,
            workspaceLease.Failure is null ? null : CodeActionWorkspaceResultMapper.MapFailure(workspaceLease.Failure));
    }

    public CodeActionMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateMutationContext(request.Workspace, cancellationToken);
        var context = workspaceLease.Context is null
            ? null
            : new CodeActionMutationContext(workspaceLease.Context);
        return new CodeActionMutationExecutionLease(
            workspaceLease,
            context,
            workspaceLease.Failure is null ? null : CodeActionWorkspaceResultMapper.MapFailure(workspaceLease.Failure));
    }
}
