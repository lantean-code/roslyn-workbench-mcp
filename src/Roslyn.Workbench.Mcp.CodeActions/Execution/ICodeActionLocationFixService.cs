namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionLocationFixService
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
