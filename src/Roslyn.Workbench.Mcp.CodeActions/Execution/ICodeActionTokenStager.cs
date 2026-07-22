namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionTokenStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeActionAsync(
        StageCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeFixAsync(
        StageCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
