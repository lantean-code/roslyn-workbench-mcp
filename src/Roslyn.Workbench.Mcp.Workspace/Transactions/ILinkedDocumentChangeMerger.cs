namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Reconciles candidate edits made through documents that share one physical file.
/// </summary>
internal interface ILinkedDocumentChangeMerger
{
    /// <summary>
    /// Merges compatible edits made through linked documents into the candidate solution.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the linked document change merge result.</returns>
    ValueTask<LinkedDocumentChangeMergeResult> MergeAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
