namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Preserves project and folder context when candidate documents move between paths or projects.
/// </summary>
internal interface IRelocatedDocumentProjectContextPropagator
{
    /// <summary>
    /// Applies matching project and folder context to relocated documents.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The candidate solution after context propagation.</returns>
    Solution Propagate(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
