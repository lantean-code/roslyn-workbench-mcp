namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Converts solution changes into a validated, recoverable sequence of file operations.
/// </summary>
internal interface IWorkspaceCommitPlanner
{
    /// <summary>
    /// Creates a durable commit plan from the baseline and current solutions.
    /// </summary>
    /// <param name="commitId">The commit identifier.</param>
    /// <param name="loadedPath">The path loaded into the workspace.</param>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="baselineSolution">The solution used as the comparison baseline.</param>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace commit plan result.</returns>
    ValueTask<WorkspaceCommitPlanResult> CreateAsync(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken);
}
