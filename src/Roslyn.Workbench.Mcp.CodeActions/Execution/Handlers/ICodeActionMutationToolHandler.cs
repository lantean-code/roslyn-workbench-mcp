namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

/// <summary>
/// Executes a Code Action mutation against an acquired transaction context.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
internal interface ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Executes the Code Action mutation tool.
    /// </summary>
    /// <param name="request">The validated mutation request.</param>
    /// <param name="context">The transaction-scoped Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The proposed workspace mutation or a normalized failure.</returns>
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
