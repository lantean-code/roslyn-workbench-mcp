namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ILocationCodeFixStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
