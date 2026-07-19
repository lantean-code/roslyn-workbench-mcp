namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionReplayService
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeActionAsync(
        StageCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeFixAsync(
        StageCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

#pragma warning disable CA1068 // The token precedes optional selector filters so callers need not supply unrelated options.
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageSelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken,
        ICodeActionExecutionContext context,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null);
#pragma warning restore CA1068
}
