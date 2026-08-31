namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Copies project and folder context to documents newly introduced by a mutation candidate.
/// </summary>
internal sealed class AddedDocumentProjectContextPropagator : IAddedDocumentProjectContextPropagator
{
    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddedDocumentProjectContextPropagator"/> class.
    /// </summary>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    public AddedDocumentProjectContextPropagator(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    /// <summary>
    /// Applies matching project and folder context to newly added documents.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the candidate solution after context propagation.</returns>
    public async ValueTask<Solution> PropagateAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        var propagatedSolution = candidateSolution;
        var solutionChanges = candidateSolution.GetChanges(currentSolution);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            var sourceProject = GetRequiredProject(candidateSolution, projectChanges.ProjectId);
            if (string.IsNullOrWhiteSpace(sourceProject.FilePath))
            {
                continue;
            }

            foreach (var addedDocumentId in projectChanges.GetAddedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var addedDocument = GetRequiredDocument(candidateSolution, addedDocumentId);
                var addedText = await addedDocument.GetTextAsync(cancellationToken);

                propagatedSolution = PropagateDocument(
                    propagatedSolution,
                    sourceProject,
                    addedDocument,
                    addedText);
            }
        }

        return propagatedSolution;
    }

    private Solution PropagateDocument(
        Solution solution,
        Project sourceProject,
        Document addedDocument,
        SourceText addedText)
    {
        if (string.IsNullOrWhiteSpace(addedDocument.FilePath))
        {
            return solution;
        }

        var propagatedSolution = solution;
        var siblingProjectIds = solution.Projects
            .Where(project => ProjectContextMatcher.AreSiblingContexts(
                sourceProject,
                project,
                _pathComparison))
            .Select(static project => project.Id)
            .ToArray();

        foreach (var siblingProjectId in siblingProjectIds)
        {
            var siblingProject = GetRequiredProject(propagatedSolution, siblingProjectId);
            if (ProjectContextMatcher.ContainsDocument(
                siblingProject,
                addedDocument.FilePath,
                _pathComparison))
            {
                continue;
            }

            var siblingDocumentId = DocumentId.CreateNewId(siblingProjectId);
            propagatedSolution = propagatedSolution.AddDocument(
                siblingDocumentId,
                addedDocument.Name,
                addedText,
                addedDocument.Folders,
                addedDocument.FilePath);
        }

        return propagatedSolution;
    }

    private static Project GetRequiredProject(Solution solution, ProjectId projectId)
    {
        return solution.GetProject(projectId)
            ?? throw new InvalidOperationException(
                $"The project '{projectId}' is not present in the candidate solution.");
    }

    private static Document GetRequiredDocument(Solution solution, DocumentId documentId)
    {
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The document '{documentId}' is not present in the candidate solution.");
    }
}
