namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionMutationContext : IMutationContext
{
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageReplaySelectionAsync(
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
