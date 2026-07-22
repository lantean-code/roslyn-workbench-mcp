namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface IScopedCodeFixStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
