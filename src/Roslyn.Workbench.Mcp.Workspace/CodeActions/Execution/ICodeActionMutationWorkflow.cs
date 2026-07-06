namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution;

internal interface ICodeActionMutationWorkflow
{
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);
}
