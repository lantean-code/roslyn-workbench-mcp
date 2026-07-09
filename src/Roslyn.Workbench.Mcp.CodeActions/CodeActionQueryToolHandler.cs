using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions;

internal abstract class CodeActionQueryToolHandler<TRequest, TResponse> : IQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(
        TRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        if (context is not ICodeActionQueryContext codeActionContext)
        {
            throw new InvalidOperationException("Query context does not support code-action execution.");
        }

        return ExecuteCoreAsync(request, codeActionContext, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<TResponse>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
