namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceCommitWriter
{
    ValueTask ApplyAsync(
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken);
}
