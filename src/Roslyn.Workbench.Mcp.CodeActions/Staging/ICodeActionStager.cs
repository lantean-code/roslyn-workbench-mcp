namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal interface ICodeActionStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageAsync(
        StageCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
