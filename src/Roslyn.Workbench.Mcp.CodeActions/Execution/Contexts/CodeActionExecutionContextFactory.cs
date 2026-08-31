namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Adapts neutral workspace leases into query and mutation contexts for Code Action tools.
/// </summary>
internal sealed class CodeActionExecutionContextFactory : ICodeActionExecutionContextFactory
{
    private readonly IWorkspaceExecutionContextFactory _workspaceFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionExecutionContextFactory"/> class.
    /// </summary>
    /// <param name="workspaceFactory">The factory that acquires neutral workspace execution leases.</param>
    public CodeActionExecutionContextFactory(IWorkspaceExecutionContextFactory workspaceFactory)
    {
        _workspaceFactory = workspaceFactory;
    }

    /// <summary>
    /// Acquires a read-only Code Action context for a workspace-bound request.
    /// </summary>
    /// <param name="request">The request identifying the target workspace.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An acquired query lease or a normalized workspace failure.</returns>
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

    /// <summary>
    /// Acquires a transaction-scoped Code Action context for a mutation request.
    /// </summary>
    /// <param name="request">The request identifying the workspace and expected snapshot.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An acquired mutation lease or a normalized workspace failure.</returns>
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
