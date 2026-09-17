namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Creates stable, Workspace-relative receipt entries from exact commit-plan operations.
/// </summary>
internal sealed class TransactionReviewDocumentFactory : ITransactionReviewDocumentFactory
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IFileSystem _fileSystem;
    private readonly IGeneratedSourceClassifier _generatedSourceClassifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReviewDocumentFactory"/> class.
    /// </summary>
    /// <param name="pathComparison">The platform-aware Workspace path comparison.</param>
    /// <param name="fileSystem">The file-system abstraction used for relative path calculations.</param>
    /// <param name="generatedSourceClassifier">The shared generated-source classifier.</param>
    public TransactionReviewDocumentFactory(
        IWorkspacePathComparison pathComparison,
        IFileSystem fileSystem,
        IGeneratedSourceClassifier generatedSourceClassifier)
    {
        _pathComparison = pathComparison;
        _fileSystem = fileSystem;
        _generatedSourceClassifier = generatedSourceClassifier;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<TransactionReviewDocument>> CreateAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceCommitPlan plan,
        ChangeSummary? changes,
        CancellationToken cancellationToken)
    {
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("Review document projection requires an active transaction.");

        var lineSummaries = CreateLineSummaryMap(session.Workspace.WorkspaceRoot, changes);
        var sourceDocuments = CreateSourceDocumentMap(transaction);
        var documents = new List<TransactionReviewDocument>(plan.Manifest.Entries.Count);
        foreach (var entry in plan.Manifest.Entries)
        {
            var document = await CreateDocumentAsync(
                session,
                transaction,
                entry,
                lineSummaries,
                sourceDocuments,
                cancellationToken);

            documents.Add(document);
        }

        return documents
            .OrderBy(static document => document.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<TransactionReviewDocument> CreateDocumentAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        WorkspaceCommitEntry entry,
        Dictionary<FileSystemPathKey, DiffSummary> lineSummaries,
        Dictionary<FileSystemPathKey, Document> sourceDocuments,
        CancellationToken cancellationToken)
    {
        var relativePath = CreateRelativePath(session.Workspace.WorkspaceRoot, entry.TargetPath);
        var pathKey = _pathComparison.CreateKey(entry.TargetPath);
        lineSummaries.TryGetValue(pathKey, out var lineSummary);
        var classification = await GetClassificationAsync(
            session,
            pathKey,
            sourceDocuments,
            cancellationToken);

        return new TransactionReviewDocument
        {
            Path = relativePath,
            Operation = entry.Operation,
            OriginalExists = entry.OriginalExists,
            OriginalHash = entry.OriginalHash,
            IntendedHash = entry.IntendedHash,
            IntendedUnixFileMode = (int?)entry.IntendedUnixFileMode,
            Projects = CreateProjectOwners(session, transaction, entry.TargetPath),
            Classification = classification,
            LineSummary = lineSummary,
        };
    }

    private async ValueTask<string> GetClassificationAsync(
        WorkspaceSessionSnapshot session,
        FileSystemPathKey pathKey,
        Dictionary<FileSystemPathKey, Document> sourceDocuments,
        CancellationToken cancellationToken)
    {
        if (!sourceDocuments.TryGetValue(pathKey, out var document))
        {
            return "project-source";
        }

        var isGeneratedLooking = await _generatedSourceClassifier.IsGeneratedLookingAsync(
            document,
            session.Workspace.WorkspaceRoot,
            cancellationToken);

        return isGeneratedLooking
            ? "generated-looking-source"
            : "project-source";
    }

    private Dictionary<FileSystemPathKey, Document> CreateSourceDocumentMap(WorkspaceTransaction transaction)
    {
        var documents = new Dictionary<FileSystemPathKey, Document>();
        AddDocuments(documents, transaction.CurrentSolution);
        AddDocuments(documents, transaction.BaselineSolution);
        return documents;
    }

    private void AddDocuments(Dictionary<FileSystemPathKey, Document> documents, Solution solution)
    {
        foreach (var document in solution.Projects.SelectMany(static project => project.Documents))
        {
            if (document.FilePath is not null)
            {
                documents.TryAdd(_pathComparison.CreateKey(document.FilePath), document);
            }
        }
    }

    private TransactionReviewProject[] CreateProjectOwners(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction,
        string targetPath)
    {
        var targetKey = _pathComparison.CreateKey(targetPath);
        var owners = new HashSet<TransactionReviewProject>();
        foreach (var project in transaction.BaselineSolution.Projects.Concat(transaction.CurrentSolution.Projects))
        {
            if (!ProjectContainsPath(project, targetKey) || string.IsNullOrWhiteSpace(project.FilePath))
            {
                continue;
            }

            owners.Add(new TransactionReviewProject
            {
                Path = CreateRelativePath(session.Workspace.WorkspaceRoot, project.FilePath),
                TargetFramework = session.ProjectTargetFrameworks.GetTargetFramework(project.Id),
            });
        }

        return owners
            .OrderBy(static project => project.Path, StringComparer.Ordinal)
            .ThenBy(static project => project.TargetFramework, StringComparer.Ordinal)
            .ToArray();
    }

    private bool ProjectContainsPath(Project project, FileSystemPathKey targetKey)
    {
        return project.Documents.Any(document =>
            document.FilePath is not null
            && _pathComparison.CreateKey(document.FilePath) == targetKey);
    }

    private Dictionary<FileSystemPathKey, DiffSummary> CreateLineSummaryMap(
        string workspaceRoot,
        ChangeSummary? changes)
    {
        var summaries = new Dictionary<FileSystemPathKey, DiffSummary>();
        if (changes is null)
        {
            return summaries;
        }

        foreach (var change in changes.Added.Concat(changes.Modified).Concat(changes.Deleted))
        {
            if (change.Document is null || change.Preview is null)
            {
                continue;
            }

            var absolutePath = _fileSystem.Path.Combine(workspaceRoot, change.Document.Path);
            summaries[_pathComparison.CreateKey(absolutePath)] = change.Preview;
        }

        return summaries;
    }

    private string CreateRelativePath(string workspaceRoot, string path)
    {
        return _fileSystem.Path
            .GetRelativePath(workspaceRoot, path)
            .Replace('\\', '/');
    }
}
