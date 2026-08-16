namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationCandidatePrecondition
{
    public required WorkspaceMutationCandidateIdentity ExpectedIdentity { get; init; }

    public required int MaximumChangedDocuments { get; init; }
}
