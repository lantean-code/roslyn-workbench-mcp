namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

internal interface ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    ValueTask<CodeActionExecutionResult<TResponse>> ExecuteAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
