namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

internal interface ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
