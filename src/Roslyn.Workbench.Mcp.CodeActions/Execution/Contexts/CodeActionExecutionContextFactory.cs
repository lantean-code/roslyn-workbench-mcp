namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

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
        if (workspaceLease.HasFailure)
        {
            var context = workspaceLease.Context is null
                ? null
                : new CodeActionQueryContext(workspaceLease.Context);

            return CodeActionQueryExecutionLease.Rejected(
                workspaceLease,
                CodeActionWorkspaceResultMapper.MapFailure(workspaceLease.Failure),
                context);
        }

        return CodeActionQueryExecutionLease.Acquired(
            workspaceLease,
            new CodeActionQueryContext(workspaceLease.Context));
    }

    public CodeActionMutationExecutionLease CreateMutationContext(
        WorkspaceMutationRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateMutationContext(
            request.Workspace,
            request.ExpectedSnapshot,
            cancellationToken);
        if (workspaceLease.HasFailure)
        {
            var context = workspaceLease.Context is null
                ? null
                : new CodeActionMutationContext(workspaceLease.Context);

            return CodeActionMutationExecutionLease.Rejected(
                workspaceLease,
                CodeActionWorkspaceResultMapper.MapFailure(workspaceLease.Failure),
                context);
        }

        return CodeActionMutationExecutionLease.Acquired(
            workspaceLease,
            new CodeActionMutationContext(workspaceLease.Context));
    }
}
