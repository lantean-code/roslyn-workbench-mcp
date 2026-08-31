namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Copies project and folder context to documents newly introduced by a mutation candidate.
/// </summary>
internal interface IAddedDocumentProjectContextPropagator
{
    /// <summary>
    /// Applies matching project and folder context to newly added documents.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the candidate solution after context propagation.</returns>
    ValueTask<Solution> PropagateAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
