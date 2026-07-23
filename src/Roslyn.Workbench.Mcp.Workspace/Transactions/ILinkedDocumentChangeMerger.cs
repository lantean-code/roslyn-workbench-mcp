namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface ILinkedDocumentChangeMerger
{
    ValueTask<LinkedDocumentChangeMergeResult> MergeAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
