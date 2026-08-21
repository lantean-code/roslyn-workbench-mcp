namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IRelocatedDocumentProjectContextPropagator
{
    Solution Propagate(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
