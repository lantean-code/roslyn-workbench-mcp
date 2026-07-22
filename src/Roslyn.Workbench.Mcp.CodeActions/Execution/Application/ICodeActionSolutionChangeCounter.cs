namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal interface ICodeActionSolutionChangeCounter
{
    ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);
}
