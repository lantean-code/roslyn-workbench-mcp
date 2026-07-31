namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IRemovedDocumentProjectContextPropagator
{
    Solution Propagate(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
