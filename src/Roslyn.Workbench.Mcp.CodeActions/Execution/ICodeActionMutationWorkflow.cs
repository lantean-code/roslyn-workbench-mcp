namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionMutationWorkflow
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
