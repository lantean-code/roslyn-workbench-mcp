namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Propagates removals to linked project contexts that represent the same physical document.
/// </summary>
internal interface IRemovedDocumentProjectContextPropagator
{
    /// <summary>
    /// Removes matching document contexts that should follow a candidate removal.
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
