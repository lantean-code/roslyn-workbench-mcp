namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Propagates removals to linked project contexts that represent the same physical document.
/// </summary>
internal sealed class RemovedDocumentProjectContextPropagator : IRemovedDocumentProjectContextPropagator
{
    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemovedDocumentProjectContextPropagator"/> class.
    /// </summary>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    public RemovedDocumentProjectContextPropagator(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    /// <summary>
    /// Removes matching document contexts that should follow a candidate removal.
    /// </summary>
    /// <param name="currentSolution">The solution snapshot on which the operation runs.</param>
    /// <param name="candidateSolution">The candidate solution containing the proposed changes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The candidate solution after context propagation.</returns>
    public Solution Propagate(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        var propagatedSolution = candidateSolution;
        var solutionChanges = candidateSolution.GetChanges(currentSolution);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            var sourceProject = GetRequiredProject(currentSolution, projectChanges.ProjectId);
            if (string.IsNullOrWhiteSpace(sourceProject.FilePath))
            {
                continue;
            }

            foreach (var removedDocumentId in projectChanges.GetRemovedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var removedDocument = GetRequiredDocument(currentSolution, removedDocumentId);
                propagatedSolution = PropagateDocumentRemoval(
                    propagatedSolution,
                    sourceProject,
                    removedDocument);
            }
        }

        return propagatedSolution;
    }

    private Solution PropagateDocumentRemoval(
        Solution solution,
        Project sourceProject,
        Document removedDocument)
    {
        if (string.IsNullOrWhiteSpace(removedDocument.FilePath))
        {
            return solution;
        }

        var propagatedSolution = solution;
        var siblingDocumentIds = solution.Projects
            .Where(project => ProjectContextMatcher.AreSiblingContexts(
                sourceProject,
                project,
                _pathComparison))
            .SelectMany(project => ProjectContextMatcher.GetDocumentIds(
                project,
                removedDocument.FilePath,
                _pathComparison))
            .ToArray();

        foreach (var siblingDocumentId in siblingDocumentIds)
        {
            propagatedSolution = propagatedSolution.RemoveDocument(siblingDocumentId);
        }

        return propagatedSolution;
    }

    private static Project GetRequiredProject(Solution solution, ProjectId projectId)
    {
        return solution.GetProject(projectId)
            ?? throw new InvalidOperationException(
                $"The project '{projectId}' is not present in the current solution.");
    }

    private static Document GetRequiredDocument(Solution solution, DocumentId documentId)
    {
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The document '{documentId}' is not present in the current solution.");
    }
}
