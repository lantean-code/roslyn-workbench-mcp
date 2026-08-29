namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationCandidateValidator : IWorkspaceMutationCandidateValidator
{
    private readonly IAddressableDocumentEligibility _addressableDocumentEligibility;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly IWorkspacePathComparison _pathComparison;

    public WorkspaceMutationCandidateValidator(
        IAddressableDocumentEligibility addressableDocumentEligibility,
        IPhysicalPathContainment pathContainment,
        IWorkspacePathComparison pathComparison)
    {
        _addressableDocumentEligibility = addressableDocumentEligibility;
        _pathContainment = pathContainment;
        _pathComparison = pathComparison;
    }

    public WorkspaceMutationCandidateValidationResult Validate(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot)
    {
        if (!ReferenceEquals(candidateSolution.Workspace, currentSolution.Workspace))
        {
            return CreateInvalidResult("InvalidMutationProposal", "Mutation proposals must belong to the current workspace.");
        }

        if (candidateSolution.ProjectIds.Count != currentSolution.ProjectIds.Count)
        {
            return CreateInvalidResult("UnsupportedChange", "Mutation proposals must not add or remove projects.");
        }

        foreach (var currentProject in currentSolution.Projects)
        {
            var validation = ValidateProject(
                currentProject,
                candidateSolution.GetProject(currentProject.Id),
                workspaceRoot);

            if (!validation.IsValid)
            {
                return validation;
            }
        }

        return WorkspaceMutationCandidateValidationResult.Valid();
    }

    private WorkspaceMutationCandidateValidationResult ValidateProject(
        Project currentProject,
        Project? candidateProject,
        string workspaceRoot)
    {
        if (candidateProject is null)
        {
            return CreateInvalidResult("UnsupportedChange", "Mutation proposals must not alter project identity.");
        }

        if (HasDifferentIdentity(currentProject, candidateProject))
        {
            return CreateInvalidResult("UnsupportedChange", "Mutation proposals must not alter project identity or options.");
        }

        var projectChanges = candidateProject.GetChanges(currentProject);
        if (HasReferenceOrNonSourceDocumentChanges(projectChanges))
        {
            return CreateInvalidResult("UnsupportedChange", "Mutation proposals must not alter project references or non-source documents.");
        }

        if (HasDifferentOptions(currentProject, candidateProject))
        {
            return CreateInvalidResult("UnsupportedChange", "Mutation proposals must not alter project identity or options.");
        }

        var changedDocumentIds = projectChanges.GetChangedDocuments().ToArray();
        foreach (var changedDocumentId in changedDocumentIds)
        {
            var metadataValidation = ValidateChangedDocumentMetadata(
                GetRequiredDocument(currentProject, changedDocumentId),
                GetRequiredDocument(candidateProject, changedDocumentId));

            if (!metadataValidation.IsValid)
            {
                return metadataValidation;
            }
        }

        return ValidateSourceDocumentChanges(
            currentProject,
            candidateProject,
            projectChanges,
            changedDocumentIds,
            workspaceRoot);
    }

    private WorkspaceMutationCandidateValidationResult ValidateSourceDocumentChanges(
        Project currentProject,
        Project candidateProject,
        ProjectChanges projectChanges,
        IReadOnlyList<DocumentId> changedDocumentIds,
        string workspaceRoot)
    {
        var validation = ValidateSourceDocuments(
            currentProject,
            projectChanges.GetRemovedDocuments(),
            "deleted",
            workspaceRoot,
            requireProjectDirectory: false);

        if (!validation.IsValid)
        {
            return validation;
        }

        validation = ValidateSourceDocuments(
            candidateProject,
            projectChanges.GetAddedDocuments(),
            "created",
            workspaceRoot,
            requireProjectDirectory: true);

        if (!validation.IsValid)
        {
            return validation;
        }

        return ValidateSourceDocuments(
            candidateProject,
            changedDocumentIds,
            "changed",
            workspaceRoot,
            requireProjectDirectory: false);
    }

    private WorkspaceMutationCandidateValidationResult ValidateSourceDocuments(
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
                return CreateInvalidResult("UnsupportedChange", $"Mutation proposals must use regular source documents for {operation} files.");
            }

            if (!_addressableDocumentEligibility.IsAddressable(document))
            {
                return CreateInvalidResult("UnsupportedChange", "Mutation proposals must not alter intermediate build documents.");
            }

            if (!_pathContainment.TryGetStrictlyContainedPath(
                workspaceRoot,
                document.FilePath,
                out _))
            {
                return CreateInvalidResult("UnsupportedChange", "Mutation proposals must keep mutable source files within the workspace root.");
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
                    return CreateInvalidResult("UnsupportedChange", "Mutation proposals must keep created source files within the owning project directory.");
                }
            }
        }

        return WorkspaceMutationCandidateValidationResult.Valid();
    }

    private WorkspaceMutationCandidateValidationResult ValidateChangedDocumentMetadata(
        Document currentDocument,
        Document candidateDocument)
    {
        var currentPath = currentDocument.FilePath;
        var candidatePath = candidateDocument.FilePath;
        if (string.Equals(currentPath, candidatePath, StringComparison.Ordinal))
        {
            return HasSameNonPathMetadata(currentDocument, candidateDocument)
                ? WorkspaceMutationCandidateValidationResult.Valid()
                : CreateInvalidResult("UnsupportedChange", "Mutation proposals must not alter source document metadata.");
        }

        if (string.IsNullOrWhiteSpace(currentPath)
            || string.IsNullOrWhiteSpace(candidatePath)
            || currentDocument.SourceCodeKind != SourceCodeKind.Regular
            || candidateDocument.SourceCodeKind != SourceCodeKind.Regular)
        {
            return CreateInvalidResult("UnsupportedChange", "Mutation proposals must use regular source documents for relocated files.");
        }

        if (_pathComparison.CreateKey(currentPath) == _pathComparison.CreateKey(candidatePath))
        {
            return CreateInvalidResult(
                "UnsupportedChange",
                "Case-only source file renames are not supported on a case-insensitive filesystem.");
        }

        if (!HaveSameDirectory(currentPath, candidatePath)
            || !currentDocument.Folders.SequenceEqual(candidateDocument.Folders, StringComparer.Ordinal)
            || !string.Equals(Path.GetFileName(candidatePath), candidateDocument.Name, StringComparison.Ordinal))
        {
            return CreateInvalidResult(
                "UnsupportedChange",
                "Mutation proposals may rename source files but must not move them between directories or alter their logical folders.");
        }

        return WorkspaceMutationCandidateValidationResult.Valid();
    }

    private bool HaveSameDirectory(string currentPath, string candidatePath)
    {
        var currentDirectory = Path.GetDirectoryName(currentPath);
        var candidateDirectory = Path.GetDirectoryName(candidatePath);
        if (currentDirectory is null || candidateDirectory is null)
        {
            return false;
        }

        return _pathComparison.CreateKey(currentDirectory) == _pathComparison.CreateKey(candidateDirectory);
    }

    private static bool HasSameNonPathMetadata(Document currentDocument, Document candidateDocument)
    {
        return string.Equals(currentDocument.Name, candidateDocument.Name, StringComparison.Ordinal)
            && currentDocument.SourceCodeKind == candidateDocument.SourceCodeKind
            && currentDocument.Folders.SequenceEqual(candidateDocument.Folders, StringComparer.Ordinal);
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

    private static Document GetRequiredDocument(Project project, DocumentId documentId)
    {
        return project.GetDocument(documentId)
            ?? throw new InvalidOperationException(
                $"The document '{documentId}' is not present in the expected project.");
    }

    private static WorkspaceMutationCandidateValidationResult CreateInvalidResult(string code, string message)
    {
        var error = new WorkspaceOperationError
        {
            Code = code,
            Message = message,
        };

        return WorkspaceMutationCandidateValidationResult.Invalid(error);
    }
}
