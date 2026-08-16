namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationCandidateIdentityService : IWorkspaceMutationCandidateIdentityService
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IWorkspaceDocumentContentService _documentContentService;

    public WorkspaceMutationCandidateIdentityService(
        IWorkspacePathComparison pathComparison,
        IWorkspaceDocumentContentService documentContentService)
    {
        _pathComparison = pathComparison;
        _documentContentService = documentContentService;
    }

    public async ValueTask<WorkspaceMutationCandidateIdentity> CreateAsync(
        Solution currentSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documents = new List<WorkspaceMutationDocumentIdentity>();
        var solutionChanges = candidateSolution.GetChanges(currentSolution);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentProject = currentSolution.GetProject(projectChanges.ProjectId);
            var candidateProject = candidateSolution.GetProject(projectChanges.ProjectId);

            await AddDocumentIdentitiesAsync(
                documents,
                candidateProject,
                projectChanges.GetAddedDocuments(),
                WorkspaceMutationDocumentChangeKind.Added,
                cancellationToken);

            await AddModifiedDocumentIdentitiesAsync(
                documents,
                currentProject,
                candidateProject,
                projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true),
                cancellationToken);

            await AddDocumentIdentitiesAsync(
                documents,
                currentProject,
                projectChanges.GetRemovedDocuments(),
                WorkspaceMutationDocumentChangeKind.Deleted,
                cancellationToken);
        }

        documents.Sort(WorkspaceMutationDocumentIdentityComparer.Instance);
        return new WorkspaceMutationCandidateIdentity
        {
            Documents = documents.ToArray(),
        };
    }

    public bool MatchesPrecondition(
        WorkspaceMutationCandidatePrecondition precondition,
        WorkspaceMutationCandidateIdentity candidateIdentity)
    {
        if (candidateIdentity.Documents.Count > precondition.MaximumChangedDocuments
            || candidateIdentity.Documents.Count != precondition.ExpectedIdentity.Documents.Count)
        {
            return false;
        }

        for (var index = 0; index < candidateIdentity.Documents.Count; index++)
        {
            if (candidateIdentity.Documents[index] != precondition.ExpectedIdentity.Documents[index])
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask AddModifiedDocumentIdentitiesAsync(
        List<WorkspaceMutationDocumentIdentity> identities,
        Project? currentProject,
        Project? candidateProject,
        IEnumerable<DocumentId> documentIds,
        CancellationToken cancellationToken)
    {
        if (currentProject is null || candidateProject is null)
        {
            throw new InvalidOperationException("A mutation candidate changed an unavailable project.");
        }

        foreach (var documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDocument = GetRequiredDocument(currentProject, documentId);
            var candidateDocument = GetRequiredDocument(candidateProject, documentId);
            var currentContent = await _documentContentService.CreateAsync(currentDocument, cancellationToken);
            var candidateContent = await _documentContentService.CreateAsync(candidateDocument, cancellationToken);
            if (_documentContentService.HasEquivalentContent(currentContent, candidateContent))
            {
                continue;
            }

            AddDocumentIdentity(
                identities,
                candidateProject,
                candidateDocument,
                candidateContent,
                WorkspaceMutationDocumentChangeKind.Modified);
        }
    }

    private async ValueTask AddDocumentIdentitiesAsync(
        List<WorkspaceMutationDocumentIdentity> identities,
        Project? project,
        IEnumerable<DocumentId> documentIds,
        WorkspaceMutationDocumentChangeKind changeKind,
        CancellationToken cancellationToken)
    {
        if (project is null)
        {
            throw new InvalidOperationException("A mutation candidate changed an unavailable project.");
        }

        foreach (var documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = GetRequiredDocument(project, documentId);
            var content = await _documentContentService.CreateAsync(document, cancellationToken);
            AddDocumentIdentity(identities, project, document, content, changeKind);
        }
    }

    private void AddDocumentIdentity(
        List<WorkspaceMutationDocumentIdentity> identities,
        Project project,
        Document document,
        WorkspaceDocumentContent content,
        WorkspaceMutationDocumentChangeKind changeKind)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            throw new InvalidOperationException("A mutation candidate changed a source document without a file path.");
        }

        var identity = new WorkspaceMutationDocumentIdentity
        {
            ProjectId = project.Id.Id,
            DocumentPath = _pathComparison.CreateKey(document.FilePath),
            ChangeKind = changeKind,
            ContentHash = content.ContentHash,
            SerializedBytesHash = content.SerializedBytesHash,
            EncodingName = content.EncodingName,
        };

        identities.Add(identity);
    }

    private static Document GetRequiredDocument(Project project, DocumentId documentId)
    {
        var document = project.GetDocument(documentId);
        if (document is null)
        {
            throw new InvalidOperationException("A mutation candidate changed an unavailable source document.");
        }

        return document;
    }
}
