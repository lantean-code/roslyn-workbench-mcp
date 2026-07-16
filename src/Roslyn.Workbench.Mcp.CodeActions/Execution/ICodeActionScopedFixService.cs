namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionScopedFixService
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

}
