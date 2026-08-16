namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceMutationCandidateIdentityService
{
    ValueTask<WorkspaceMutationCandidateIdentity> CreateAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);

    bool MatchesPrecondition(
        WorkspaceMutationCandidatePrecondition precondition,
        WorkspaceMutationCandidateIdentity candidateIdentity);
}
