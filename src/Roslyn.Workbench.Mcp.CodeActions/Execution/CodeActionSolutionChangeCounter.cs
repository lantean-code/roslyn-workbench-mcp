namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionSolutionChangeCounter : ICodeActionSolutionChangeCounter
{
    public async ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var document in before.Projects.SelectMany(static project => project.Documents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updatedDocument = after.GetDocument(document.Id);
            if (updatedDocument is null)
            {
                continue;
            }

            var originalText = await document.GetTextAsync(cancellationToken);
            var updatedText = await updatedDocument.GetTextAsync(cancellationToken);
            if (!originalText.ContentEquals(updatedText))
            {
                count++;
            }
        }

        return count;
    }
}
