namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

/// <summary>
/// Identifies and counts source documents changed by a candidate solution.
/// </summary>
internal interface ICodeActionSolutionChangeCounter
{
    /// <summary>
    /// Gets added, removed, or content-modified source documents.
    /// </summary>
    /// <param name="before">The solution state before the proposed change.</param>
    /// <param name="after">The solution state after the proposed change.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the changed source documents.</returns>
    ValueTask<IReadOnlyList<Document>> GetChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts the changed source documents.
    /// </summary>
    /// <param name="before">The solution state before the proposed change.</param>
    /// <param name="after">The solution state after the proposed change.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the number of changed source documents.</returns>
    ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);
}
