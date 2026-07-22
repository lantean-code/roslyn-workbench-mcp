namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionFixAllStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
