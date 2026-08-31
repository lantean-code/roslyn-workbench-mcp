namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies a mutation candidate by the exact documents and serialized content it changes.
/// </summary>
internal sealed record WorkspaceMutationCandidateIdentity
{
    /// <summary>
    /// Gets the deterministic set of changed-document identities.
    /// </summary>
    public required IReadOnlyList<WorkspaceMutationDocumentIdentity> Documents { get; init; }
}
