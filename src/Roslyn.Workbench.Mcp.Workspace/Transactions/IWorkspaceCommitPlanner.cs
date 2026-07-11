namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceCommitPlanner
{
    ValueTask<WorkspaceCommitPlan> CreateAsync(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken);
}
