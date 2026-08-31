namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Normalises linked and project-context document changes before a candidate is staged.
/// </summary>
internal interface IWorkspaceMutationCandidateProcessor
{
    /// <summary>
    /// Propagates project context, merges linked changes, and validates the resulting candidate.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace mutation candidate processing result.</returns>
    ValueTask<WorkspaceMutationCandidateProcessingResult> ProcessAsync(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot,
        CancellationToken cancellationToken);
}
