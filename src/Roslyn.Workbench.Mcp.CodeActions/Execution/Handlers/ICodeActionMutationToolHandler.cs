namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

internal interface ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
