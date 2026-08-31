namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Records the scope and mutation precondition needed to replay a prepared Fix All operation.
/// </summary>
internal sealed record PreparedFixAllReplayData
{
    /// <summary>
    /// Gets the scope over which Fix All was prepared.
    /// </summary>
    public required CodeActionFixAllScope Scope { get; init; }

    /// <summary>
    /// Gets the precondition that the candidate solution must satisfy before the operation can be replayed.
    /// </summary>
    public required WorkspaceMutationCandidatePrecondition CandidatePrecondition { get; init; }
}
