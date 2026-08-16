namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationCandidate
{
    public required Solution CandidateSolution { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    public WorkspaceMutationCandidatePrecondition? Precondition { get; init; }
}
