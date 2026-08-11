namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal sealed class CodeActionSolutionChangeCounter : ICodeActionSolutionChangeCounter
{
    public async ValueTask<IReadOnlyList<Document>> GetChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidateDocumentIds = GetCandidateDocumentIds(before, after);
        var changedDocuments = new List<Document>();
        var beforeDocumentIds = before.Projects
            .SelectMany(static project => project.DocumentIds)
            .ToArray();

        var afterDocumentIds = after.Projects
            .SelectMany(static project => project.DocumentIds)
            .ToArray();

        var documentIds = beforeDocumentIds
            .Concat(afterDocumentIds)
            .Distinct()
            .ToArray();

        foreach (var documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!candidateDocumentIds.Contains(documentId))
            {
                continue;
            }

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

    private static HashSet<DocumentId> GetCandidateDocumentIds(Solution before, Solution after)
    {
        var candidateDocumentIds = new HashSet<DocumentId>();
        var solutionChanges = after.GetChanges(before);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            candidateDocumentIds.UnionWith(projectChanges.GetAddedDocuments());
            candidateDocumentIds.UnionWith(projectChanges.GetRemovedDocuments());
            candidateDocumentIds.UnionWith(projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true));
        }

        foreach (var project in solutionChanges.GetAddedProjects())
        {
            candidateDocumentIds.UnionWith(project.DocumentIds);
        }

        foreach (var project in solutionChanges.GetRemovedProjects())
        {
            candidateDocumentIds.UnionWith(project.DocumentIds);
        }

        return candidateDocumentIds;
    }
}
