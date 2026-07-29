namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceMutationCandidateProcessor
{
    ValueTask<WorkspaceMutationCandidateProcessingResult> ProcessAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);
}
