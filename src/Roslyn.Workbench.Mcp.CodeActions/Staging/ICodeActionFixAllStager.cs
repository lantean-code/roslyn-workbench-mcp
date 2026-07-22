namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal interface ICodeActionFixAllStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
