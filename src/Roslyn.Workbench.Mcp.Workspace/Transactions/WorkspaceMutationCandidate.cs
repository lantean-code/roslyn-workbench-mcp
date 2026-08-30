namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationCandidate
{
    public required Solution CandidateSolution { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    public WorkspaceMutationCandidatePrecondition? Precondition { get; init; }
}
