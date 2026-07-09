namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionMutationWorkflow
{
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
