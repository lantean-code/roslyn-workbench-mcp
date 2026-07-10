namespace Roslyn.Workbench.Mcp.CodeActions;

internal abstract class CodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<CodeActionExecutionResult<TResponse>> ExecuteAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<CodeActionExecutionResult<TResponse>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
