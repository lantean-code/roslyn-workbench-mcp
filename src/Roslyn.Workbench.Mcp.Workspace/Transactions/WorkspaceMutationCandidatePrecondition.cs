namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Constrains a mutation candidate by its expected document identity and maximum change count.
/// </summary>
internal sealed record WorkspaceMutationCandidatePrecondition
{
    /// <summary>
    /// Gets the expected changed-document identities, when exact candidate matching is required.
    /// </summary>
    public required WorkspaceMutationCandidateIdentity ExpectedIdentity { get; init; }

    /// <summary>
    /// Gets the maximum number of documents the candidate may change.
    /// </summary>
    public required int MaximumChangedDocuments { get; init; }
}
