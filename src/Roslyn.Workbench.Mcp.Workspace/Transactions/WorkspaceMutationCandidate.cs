namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Carries a proposed solution change and the constraints under which it may be staged.
/// </summary>
internal sealed record WorkspaceMutationCandidate
{
    /// <summary>
    /// Gets a value indicating whether the operation produced a candidate solution.
    /// </summary>
    public required Solution CandidateSolution { get; init; }

    /// <summary>
    /// Gets the concise user-facing summary of the proposed change.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets non-fatal warnings produced while preparing the candidate.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the optional candidate identity and changed-document limits required for staging.
    /// </summary>
    public WorkspaceMutationCandidatePrecondition? Precondition { get; init; }
}
