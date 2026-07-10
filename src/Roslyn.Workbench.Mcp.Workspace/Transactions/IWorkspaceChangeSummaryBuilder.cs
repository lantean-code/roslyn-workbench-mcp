namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceChangeSummaryBuilder
{
    ValueTask<ChangeSummary> CreateAsync(
        Solution baselineSolution,
        Solution currentSolution,
        IWorkspaceResolver resolver,
        CancellationToken cancellationToken);
}
