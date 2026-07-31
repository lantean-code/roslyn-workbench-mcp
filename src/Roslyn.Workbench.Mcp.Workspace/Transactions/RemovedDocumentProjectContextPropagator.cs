namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class RemovedDocumentProjectContextPropagator : IRemovedDocumentProjectContextPropagator
{
    private readonly IWorkspacePathComparison _pathComparison;

    public RemovedDocumentProjectContextPropagator(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

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
            .Where(project => IsSiblingProject(sourceProject, project))
            .SelectMany(project => GetDocumentIds(project, removedDocument.FilePath))
            .ToArray();

        foreach (var siblingDocumentId in siblingDocumentIds)
        {
            propagatedSolution = propagatedSolution.RemoveDocument(siblingDocumentId);
        }

        return propagatedSolution;
    }

    private bool IsSiblingProject(Project sourceProject, Project candidateProject)
    {
        var sourceProjectPath = sourceProject.FilePath;
        if (candidateProject.Id == sourceProject.Id
            || string.IsNullOrWhiteSpace(sourceProjectPath)
            || string.IsNullOrWhiteSpace(candidateProject.FilePath))
        {
            return false;
        }

        var comparison = _pathComparison.GetComparison(sourceProjectPath);
        return string.Equals(
            sourceProjectPath,
            candidateProject.FilePath,
            comparison);
    }

    private IEnumerable<DocumentId> GetDocumentIds(Project project, string documentPath)
    {
        var comparison = _pathComparison.GetComparison(documentPath);
        return project.Documents
            .Where(document => string.Equals(document.FilePath, documentPath, comparison))
            .Select(static document => document.Id);
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
