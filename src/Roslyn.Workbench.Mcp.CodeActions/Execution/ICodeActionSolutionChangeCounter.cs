namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionSolutionChangeCounter
{
    ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);
}
