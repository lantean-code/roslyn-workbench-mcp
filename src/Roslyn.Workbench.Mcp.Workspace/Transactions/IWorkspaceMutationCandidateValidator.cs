namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Ensures a candidate contains only supported source-document changes within the workspace root.
/// </summary>
internal interface IWorkspaceMutationCandidateValidator
{
    /// <summary>
    /// Validates the shape, paths, and document changes in a candidate solution.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <returns>The workspace mutation candidate validation result.</returns>
    WorkspaceMutationCandidateValidationResult Validate(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot);
}
