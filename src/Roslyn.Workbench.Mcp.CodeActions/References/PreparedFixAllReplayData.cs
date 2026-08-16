namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed record PreparedFixAllReplayData
{
    public required CodeActionFixAllScope Scope { get; init; }

    public required WorkspaceMutationCandidatePrecondition CandidatePrecondition { get; init; }
}
