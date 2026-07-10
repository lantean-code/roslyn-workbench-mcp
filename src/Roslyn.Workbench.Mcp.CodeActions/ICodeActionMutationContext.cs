namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionMutationContext : ICodeActionExecutionContext
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageReplaySelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null);
}
