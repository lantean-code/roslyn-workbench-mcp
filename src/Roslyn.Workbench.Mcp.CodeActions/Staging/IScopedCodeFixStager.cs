namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal interface IScopedCodeFixStager
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
