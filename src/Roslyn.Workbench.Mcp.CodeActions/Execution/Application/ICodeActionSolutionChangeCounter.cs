namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal interface ICodeActionSolutionChangeCounter
{
    ValueTask<IReadOnlyList<Document>> GetChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);

    ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);
}
