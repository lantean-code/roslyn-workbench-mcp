namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationCandidateIdentity
{
    public required IReadOnlyList<WorkspaceMutationDocumentIdentity> Documents { get; init; }
}
