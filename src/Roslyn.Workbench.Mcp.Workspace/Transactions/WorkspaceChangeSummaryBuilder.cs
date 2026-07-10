namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceChangeSummaryBuilder : IWorkspaceChangeSummaryBuilder
{
    public async ValueTask<ChangeSummary> CreateAsync(
        Solution baselineSolution,
        Solution currentSolution,
        IWorkspaceResolver resolver,
        CancellationToken cancellationToken)
    {
        return await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            baselineSolution,
            currentSolution,
            resolver,
            cancellationToken).ConfigureAwait(false);
    }
}
