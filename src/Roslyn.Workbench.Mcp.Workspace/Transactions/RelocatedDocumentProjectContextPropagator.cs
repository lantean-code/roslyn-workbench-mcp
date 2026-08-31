using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Preserves project and folder context when candidate documents move between paths or projects.
/// </summary>
internal sealed class RelocatedDocumentProjectContextPropagator : IRelocatedDocumentProjectContextPropagator
{
    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelocatedDocumentProjectContextPropagator"/> class.
    /// </summary>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    public RelocatedDocumentProjectContextPropagator(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    /// <summary>
    /// Applies matching project and folder context to relocated documents.
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
            var currentProject = GetRequiredProject(currentSolution, projectChanges.ProjectId);
            var currentProjectPath = currentProject.FilePath;
            if (string.IsNullOrWhiteSpace(currentProjectPath))
            {
                continue;
            }

            foreach (var changedDocumentId in projectChanges.GetChangedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDocument = GetRequiredDocument(currentSolution, changedDocumentId);
                var candidateDocument = GetRequiredDocument(candidateSolution, changedDocumentId);
                var currentDocumentPath = currentDocument.FilePath;
                var candidateDocumentPath = candidateDocument.FilePath;
                if (!HasRelocated(currentDocumentPath, candidateDocumentPath))
                {
                    continue;
                }

                propagatedSolution = PropagateRelocation(
                    currentSolution,
                    propagatedSolution,
                    currentProject,
                    currentDocumentPath,
                    candidateDocumentPath,
                    candidateDocument.Name);
            }
        }

        return propagatedSolution;
    }

    private Solution PropagateRelocation(
        Solution currentSolution,
        Solution candidateSolution,
        Project sourceProject,
        string currentDocumentPath,
        string candidateDocumentPath,
        string candidateDocumentName)
    {
        var propagatedSolution = candidateSolution;
        var siblingDocumentIds = currentSolution.Projects
            .Where(project => ProjectContextMatcher.AreSiblingContexts(
                sourceProject,
                project,
                _pathComparison))
            .SelectMany(project => ProjectContextMatcher.GetDocumentIds(
                project,
                currentDocumentPath,
                _pathComparison))
            .ToArray();

        foreach (var siblingDocumentId in siblingDocumentIds)
        {
            if (propagatedSolution.GetDocument(siblingDocumentId) is null)
            {
                continue;
            }

            propagatedSolution = propagatedSolution
                .WithDocumentFilePath(siblingDocumentId, candidateDocumentPath)
                .WithDocumentName(siblingDocumentId, candidateDocumentName);
        }

        return propagatedSolution;
    }

    private static bool HasRelocated(
        [NotNullWhen(true)] string? currentDocumentPath,
        [NotNullWhen(true)] string? candidateDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(currentDocumentPath)
            || string.IsNullOrWhiteSpace(candidateDocumentPath))
        {
            return false;
        }

        return !string.Equals(
            currentDocumentPath,
            candidateDocumentPath,
            StringComparison.Ordinal);
    }

    private static Project GetRequiredProject(Solution solution, ProjectId projectId)
    {
        return solution.GetProject(projectId)
            ?? throw new InvalidOperationException(
                $"The project '{projectId}' is not present in the expected solution.");
    }

    private static Document GetRequiredDocument(Solution solution, DocumentId documentId)
    {
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The document '{documentId}' is not present in the expected solution.");
    }
}
