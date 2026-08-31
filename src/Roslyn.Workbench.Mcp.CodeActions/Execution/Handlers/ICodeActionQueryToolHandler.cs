namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

/// <summary>
/// Executes a Code Action query against an acquired read-only context.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal interface ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Executes the Code Action query tool.
    /// </summary>
    /// <param name="request">The validated query request.</param>
    /// <param name="context">The read-only Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The query response or a normalized failure.</returns>
    ValueTask<CodeActionExecutionResult<TResponse>> ExecuteAsync(
        TRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
