namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal interface ICodeActionReferenceStager
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
