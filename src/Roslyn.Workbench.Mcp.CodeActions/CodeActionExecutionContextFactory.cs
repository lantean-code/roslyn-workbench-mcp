namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionExecutionContextFactory : ICodeActionExecutionContextFactory
{
    private readonly IWorkspaceExecutionContextFactory _workspaceFactory;
    private readonly ICodeActionQueryWorkflow _queryWorkflow;
    private readonly ICodeActionMutationWorkflow _mutationWorkflow;

    public CodeActionExecutionContextFactory(
        IWorkspaceExecutionContextFactory workspaceFactory,
        ICodeActionQueryWorkflow queryWorkflow,
        ICodeActionMutationWorkflow mutationWorkflow)
    {
        _workspaceFactory = workspaceFactory;
        _queryWorkflow = queryWorkflow;
        _mutationWorkflow = mutationWorkflow;
    }

    public CodeActionQueryExecutionLease CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateQueryContext(request.Workspace, cancellationToken);
        var context = workspaceLease.Context is null
            ? null
            : new CodeActionQueryContext(workspaceLease.Context, _queryWorkflow);
        return new CodeActionQueryExecutionLease(
            workspaceLease,
            context,
            CodeActionWorkspaceResultMapper.MapFailure(workspaceLease.Failure));
    }

    public CodeActionMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateMutationContext(request.Workspace, cancellationToken);
        var context = workspaceLease.Context is null
            ? null
            : new CodeActionMutationContext(workspaceLease.Context, _mutationWorkflow);
        return new CodeActionMutationExecutionLease(
            workspaceLease,
            context,
            CodeActionWorkspaceResultMapper.MapFailure(workspaceLease.Failure));
    }
}
