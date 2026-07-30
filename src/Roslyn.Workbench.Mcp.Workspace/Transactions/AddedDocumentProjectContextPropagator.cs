namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class AddedDocumentProjectContextPropagator : IAddedDocumentProjectContextPropagator
{
    private readonly IWorkspacePathComparison _pathComparison;

    public AddedDocumentProjectContextPropagator(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

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
            .Where(project => IsSiblingProject(sourceProject, project))
            .Select(static project => project.Id)
            .ToArray();

        foreach (var siblingProjectId in siblingProjectIds)
        {
            var siblingProject = GetRequiredProject(propagatedSolution, siblingProjectId);
            if (ContainsDocument(siblingProject, addedDocument.FilePath))
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

    private bool ContainsDocument(Project project, string documentPath)
    {
        var comparison = _pathComparison.GetComparison(documentPath);
        return project.Documents.Any(document => string.Equals(
            document.FilePath,
            documentPath,
            comparison));
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
