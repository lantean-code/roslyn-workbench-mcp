namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Calculates and compares stable identities for the documents changed by a mutation candidate.
/// </summary>
internal interface IWorkspaceMutationCandidateIdentityService
{
    /// <summary>
    /// Calculates document identities for the changes between current and candidate solutions.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace mutation candidate identity.</returns>
    ValueTask<WorkspaceMutationCandidateIdentity> CreateAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the candidate identity satisfies the supplied precondition.
    /// </summary>
    /// <param name="precondition">The snapshot precondition to compare with the candidate solution identity.</param>
    /// <param name="candidateIdentity">The candidate solution identity to compare with the snapshot precondition.</param>
    /// <returns><see langword="true"/> when every expected document identity matches the candidate; otherwise, <see langword="false"/>.</returns>
    bool MatchesPrecondition(
        WorkspaceMutationCandidatePrecondition precondition,
        WorkspaceMutationCandidateIdentity candidateIdentity);
}
