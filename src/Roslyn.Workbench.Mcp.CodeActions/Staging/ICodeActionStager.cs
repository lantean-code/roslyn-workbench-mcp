namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

/// <summary>
/// Produces transaction candidates from referenced Code Actions.
/// </summary>
internal interface ICodeActionStager
{
    /// <summary>
    /// Resolves and evaluates the requested Code Action to produce a candidate solution for staging.
    /// </summary>
    /// <param name="request">The referenced action and snapshot precondition to stage.</param>
    /// <param name="context">The current transaction-scoped Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The candidate solution or a rejection explaining why it could not be produced.</returns>
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageAsync(
        StageCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
