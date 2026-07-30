namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class LinkedDocumentChangeMerger : ILinkedDocumentChangeMerger
{
    public async ValueTask<LinkedDocumentChangeMergeResult> MergeAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        var changedDocumentIds = GetChangedDocumentIds(currentSolution, candidateSolution);
        if (changedDocumentIds.Count == 0)
        {
            return LinkedDocumentChangeMergeResult.Succeeded(candidateSolution);
        }

        var mergedSolution = candidateSolution;
        var processedDocumentIds = new HashSet<DocumentId>();
        foreach (var changedDocumentId in changedDocumentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!processedDocumentIds.Add(changedDocumentId))
            {
                continue;
            }

            var candidateDocument = GetRequiredDocument(
                candidateSolution,
                changedDocumentId);

            var relatedDocumentIds = candidateDocument.GetLinkedDocumentIds();
            if (relatedDocumentIds.Length == 0)
            {
                continue;
            }

            foreach (var relatedDocumentId in relatedDocumentIds)
            {
                processedDocumentIds.Add(relatedDocumentId);
            }

            var mergeResult = await MergeGroupAsync(
                currentSolution,
                candidateSolution,
                changedDocumentId,
                relatedDocumentIds,
                cancellationToken);

            if (!mergeResult.IsSucceeded)
            {
                return mergeResult;
            }

            var mergedDocument = GetRequiredDocument(
                mergeResult.Solution,
                changedDocumentId);

            var mergedText = await mergedDocument
                .GetTextAsync(cancellationToken);

            mergedSolution = mergedSolution.WithDocumentText(changedDocumentId, mergedText);
            foreach (var relatedDocumentId in relatedDocumentIds)
            {
                mergedSolution = mergedSolution.WithDocumentText(relatedDocumentId, mergedText);
            }
        }

        return LinkedDocumentChangeMergeResult.Succeeded(mergedSolution);
    }

    private static async ValueTask<LinkedDocumentChangeMergeResult> MergeGroupAsync(
        Solution currentSolution,
        Solution candidateSolution,
        DocumentId firstDocumentId,
        IReadOnlyList<DocumentId> relatedDocumentIds,
        CancellationToken cancellationToken)
    {
        var firstCurrentDocument = GetRequiredDocument(
            currentSolution,
            firstDocumentId);

        var baselineText = await firstCurrentDocument.GetTextAsync(cancellationToken);
        var changes = new List<TextChange>();
        var documentIds = new List<DocumentId>(relatedDocumentIds.Count + 1)
        {
            firstDocumentId,
        };

        documentIds.AddRange(relatedDocumentIds);

        foreach (var documentId in documentIds)
        {
            var currentDocument = GetRequiredDocument(
                currentSolution,
                documentId);

            var candidateDocument = GetRequiredDocument(
                candidateSolution,
                documentId);

            var documentChanges = await candidateDocument.GetTextChangesAsync(
                currentDocument,
                cancellationToken);

            changes.AddRange(documentChanges);
        }

        changes.Sort(CompareChanges);
        var mergedChanges = new List<TextChange>(changes.Count);
        foreach (var change in changes)
        {
            if (mergedChanges.Count == 0)
            {
                mergedChanges.Add(change);
                continue;
            }

            var previous = mergedChanges[^1];
            if (previous.Span == change.Span
                && string.Equals(previous.NewText, change.NewText, StringComparison.Ordinal))
            {
                continue;
            }

            if (previous.Span.IntersectsWith(change.Span))
            {
                return CreateFailure(
                    firstCurrentDocument,
                    "Linked source documents contain overlapping changes that cannot be merged safely.");
            }

            mergedChanges.Add(change);
        }

        var mergedText = baselineText.WithChanges(mergedChanges);
        var mergedSolution = candidateSolution.WithDocumentText(firstDocumentId, mergedText);
        return LinkedDocumentChangeMergeResult.Succeeded(mergedSolution);
    }

    private static List<DocumentId> GetChangedDocumentIds(
        Solution currentSolution,
        Solution candidateSolution)
    {
        var changedDocumentIds = new List<DocumentId>();
        var solutionChanges = candidateSolution.GetChanges(currentSolution);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            changedDocumentIds.AddRange(
                projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true));
        }

        return changedDocumentIds;
    }

    private static LinkedDocumentChangeMergeResult CreateFailure(
        Document document,
        string reason)
    {
        var error = new WorkspaceOperationError
        {
            Code = WorkspaceErrorCodes.LinkedDocumentConflict,
            Message = $"The linked document changes for '{document.FilePath}' could not be reconciled. {reason}",
        };

        return LinkedDocumentChangeMergeResult.Failed(error);
    }

    private static Document GetRequiredDocument(
        Solution solution,
        DocumentId documentId)
    {
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The linked document '{documentId}' is not present in the solution.");
    }

    private static int CompareChanges(TextChange left, TextChange right)
    {
        var startComparison = left.Span.Start.CompareTo(right.Span.Start);
        return startComparison != 0
            ? startComparison
            : left.Span.Length.CompareTo(right.Span.Length);
    }
}
