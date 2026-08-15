namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationCandidateValidator : IWorkspaceMutationCandidateValidator
{
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly IWorkspacePathComparison _pathComparison;

    public WorkspaceMutationCandidateValidator(
        IPhysicalPathContainment pathContainment,
        IWorkspacePathComparison pathComparison)
    {
        _pathContainment = pathContainment;
        _pathComparison = pathComparison;
    }

    public WorkspaceOperationError? Validate(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot)
    {
        if (!ReferenceEquals(candidateSolution.Workspace, currentSolution.Workspace))
        {
            return CreateError("InvalidMutationProposal", "Mutation proposals must belong to the current workspace.");
        }

        if (candidateSolution.ProjectIds.Count != currentSolution.ProjectIds.Count)
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not add or remove projects.");
        }

        foreach (var currentProject in currentSolution.Projects)
        {
            var validationError = ValidateProject(
                currentProject,
                candidateSolution.GetProject(currentProject.Id),
                workspaceRoot);
            if (validationError is not null)
            {
                return validationError;
            }
        }

        return null;
    }

    private WorkspaceOperationError? ValidateProject(
        Project currentProject,
        Project? candidateProject,
        string workspaceRoot)
    {
        if (candidateProject is null)
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not alter project identity.");
        }

        if (HasDifferentIdentity(currentProject, candidateProject))
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not alter project identity or options.");
        }

        var projectChanges = candidateProject.GetChanges(currentProject);
        if (HasReferenceOrNonSourceDocumentChanges(projectChanges))
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not alter project references or non-source documents.");
        }

        if (HasDifferentOptions(currentProject, candidateProject))
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not alter project identity or options.");
        }

        var textChangedDocuments = projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true).ToHashSet();
        if (projectChanges.GetChangedDocuments().Except(textChangedDocuments).Any())
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not alter source document metadata.");
        }

        return ValidateSourceDocumentChanges(
            currentProject,
            candidateProject,
            projectChanges,
            textChangedDocuments,
            workspaceRoot);
    }

    private WorkspaceOperationError? ValidateSourceDocumentChanges(
        Project currentProject,
        Project candidateProject,
        ProjectChanges projectChanges,
        IReadOnlySet<DocumentId> textChangedDocuments,
        string workspaceRoot)
    {
        var validationError = TryValidateSourceDocuments(
            currentProject,
            projectChanges.GetRemovedDocuments(),
            "deleted",
            workspaceRoot,
            requireProjectDirectory: false);

        if (validationError is not null)
        {
            return validationError;
        }

        validationError = TryValidateSourceDocuments(
            candidateProject,
            projectChanges.GetAddedDocuments(),
            "created",
            workspaceRoot,
            requireProjectDirectory: true);

        if (validationError is not null)
        {
            return validationError;
        }

        return TryValidateSourceDocuments(
            candidateProject,
            textChangedDocuments,
            "changed",
            workspaceRoot,
            requireProjectDirectory: false);
    }

    private WorkspaceOperationError? TryValidateSourceDocuments(
        Project project,
        IEnumerable<DocumentId> documentIds,
        string operation,
        string workspaceRoot,
        bool requireProjectDirectory)
    {
        foreach (var documentId in documentIds)
        {
            var document = project.GetDocument(documentId);
            if (document is null
                || document.SourceCodeKind != SourceCodeKind.Regular
                || string.IsNullOrWhiteSpace(document.FilePath))
            {
                return CreateError("UnsupportedChange", $"Mutation proposals must use regular source documents for {operation} files.");
            }

            if (!_pathContainment.TryGetStrictlyContainedPath(
                workspaceRoot,
                document.FilePath,
                out _))
            {
                return CreateError("UnsupportedChange", "Mutation proposals must keep mutable source files within the workspace root.");
            }

            if (requireProjectDirectory)
            {
                var projectDirectory = Path.GetDirectoryName(project.FilePath ?? string.Empty);
                if (string.IsNullOrWhiteSpace(projectDirectory)
                    || !_pathContainment.TryGetStrictlyContainedPath(
                        projectDirectory,
                        document.FilePath,
                        out _))
                {
                    return CreateError("UnsupportedChange", "Mutation proposals must keep created source files within the owning project directory.");
                }
            }
        }

        return null;
    }

    private bool HasDifferentIdentity(Project currentProject, Project candidateProject)
    {
        return !PathsEqual(candidateProject.FilePath, currentProject.FilePath)
            || !string.Equals(candidateProject.Name, currentProject.Name, StringComparison.Ordinal)
            || !string.Equals(candidateProject.AssemblyName, currentProject.AssemblyName, StringComparison.Ordinal)
            || !string.Equals(candidateProject.DefaultNamespace, currentProject.DefaultNamespace, StringComparison.Ordinal);
    }

    private bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return _pathComparison.CreateKey(left) == _pathComparison.CreateKey(right);
    }

    private static bool HasDifferentOptions(Project currentProject, Project candidateProject)
    {
        return !Equals(candidateProject.CompilationOptions, currentProject.CompilationOptions)
            || !Equals(candidateProject.ParseOptions, currentProject.ParseOptions);
    }

    private static bool HasReferenceOrNonSourceDocumentChanges(ProjectChanges projectChanges)
    {
        return projectChanges.GetAddedMetadataReferences().Any()
            || projectChanges.GetRemovedMetadataReferences().Any()
            || projectChanges.GetAddedProjectReferences().Any()
            || projectChanges.GetRemovedProjectReferences().Any()
            || projectChanges.GetAddedAnalyzerReferences().Any()
            || projectChanges.GetRemovedAnalyzerReferences().Any()
            || projectChanges.GetAddedAdditionalDocuments().Any()
            || projectChanges.GetChangedAdditionalDocuments().Any()
            || projectChanges.GetRemovedAdditionalDocuments().Any()
            || projectChanges.GetAddedAnalyzerConfigDocuments().Any()
            || projectChanges.GetChangedAnalyzerConfigDocuments().Any()
            || projectChanges.GetRemovedAnalyzerConfigDocuments().Any();
    }

    private static WorkspaceOperationError CreateError(string code, string message)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = message,
        };
    }
}
