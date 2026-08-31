namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Handlers;

/// <summary>
/// Provides cancellation enforcement and the execution template for Code Action mutation handlers.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
internal abstract class CodeActionMutationToolHandler<TRequest> : ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Executes the Code Action mutation tool.
    /// </summary>
    /// <param name="request">The validated mutation request.</param>
    /// <param name="context">The transaction-scoped Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The proposed workspace mutation or a normalized failure.</returns>
    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    /// <summary>
    /// Implements the mutation after common cancellation checks have run.
    /// </summary>
    /// <param name="request">The validated mutation request.</param>
    /// <param name="context">The transaction-scoped Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The proposed workspace mutation or a normalized failure.</returns>
    protected abstract ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
