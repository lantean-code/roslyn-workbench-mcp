namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal sealed class CodeActionSolutionChangeCounter : ICodeActionSolutionChangeCounter
{
    public async ValueTask<IReadOnlyList<Document>> GetChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        var changedDocuments = new List<Document>();
        var documentIds = before.Projects
            .SelectMany(static project => project.DocumentIds)
            .Concat(after.Projects.SelectMany(static project => project.DocumentIds))
            .Distinct()
            .ToArray();

        foreach (var documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var originalDocument = before.GetDocument(documentId);
            var updatedDocument = after.GetDocument(documentId);
            if (originalDocument is null || updatedDocument is null)
            {
                var changedDocument = updatedDocument ?? originalDocument;
                if (changedDocument is not null)
                {
                    changedDocuments.Add(changedDocument);
                }

                continue;
            }

            var originalText = await originalDocument.GetTextAsync(cancellationToken);
            var updatedText = await updatedDocument.GetTextAsync(cancellationToken);
            if (!originalText.ContentEquals(updatedText))
            {
                changedDocuments.Add(updatedDocument);
            }
        }

        return changedDocuments;
    }

    public async ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        var changedDocuments = await GetChangedSourceDocumentsAsync(
            before,
            after,
            cancellationToken);

        return changedDocuments.Count;
    }
}
