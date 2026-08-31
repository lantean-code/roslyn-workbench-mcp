namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

/// <summary>
/// Provides cancellation enforcement and the execution template for Code Action query handlers.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal abstract class CodeActionQueryToolHandler<TRequest, TResponse> : ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Executes the Code Action query tool.
    /// </summary>
    /// <param name="request">The validated query request.</param>
    /// <param name="context">The read-only Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The query response or a normalized failure.</returns>
    public ValueTask<CodeActionExecutionResult<TResponse>> ExecuteAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    /// <summary>
    /// Implements the query after common cancellation checks have run.
    /// </summary>
    /// <param name="request">The validated query request.</param>
    /// <param name="context">The read-only Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The query response or a normalized failure.</returns>
    protected abstract ValueTask<CodeActionExecutionResult<TResponse>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
