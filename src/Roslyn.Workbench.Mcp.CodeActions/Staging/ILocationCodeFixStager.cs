namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal interface ILocationCodeFixStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
