namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionFixAllService
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
