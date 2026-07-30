namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IAddedDocumentProjectContextPropagator
{
    ValueTask<Solution> PropagateAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
