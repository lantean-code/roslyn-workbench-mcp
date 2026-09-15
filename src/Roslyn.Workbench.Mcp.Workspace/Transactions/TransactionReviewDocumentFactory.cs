namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Creates stable, Workspace-relative receipt entries from exact commit-plan operations.
/// </summary>
internal sealed class TransactionReviewDocumentFactory : ITransactionReviewDocumentFactory
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReviewDocumentFactory"/> class.
    /// </summary>
    /// <param name="pathComparison">The platform-aware Workspace path comparison.</param>
    /// <param name="fileSystem">The file-system abstraction used for relative path calculations.</param>
    public TransactionReviewDocumentFactory(
        IWorkspacePathComparison pathComparison,
        IFileSystem fileSystem)
    {
        _pathComparison = pathComparison;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public IReadOnlyList<TransactionReviewDocument> Create(
        WorkspaceSessionSnapshot session,
        WorkspaceCommitPlan plan,
        ChangeSummary? changes)
    {
        var lineSummaries = CreateLineSummaryMap(session.Workspace.WorkspaceRoot, changes);

        return plan.Manifest.Entries
            .Select(entry => CreateDocument(session, entry, lineSummaries))
            .OrderBy(static document => document.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private TransactionReviewDocument CreateDocument(
        WorkspaceSessionSnapshot session,
        WorkspaceCommitEntry entry,
        Dictionary<FileSystemPathKey, DiffSummary> lineSummaries)
    {
        var relativePath = CreateRelativePath(session.Workspace.WorkspaceRoot, entry.TargetPath);
        var pathKey = _pathComparison.CreateKey(entry.TargetPath);
        lineSummaries.TryGetValue(pathKey, out var lineSummary);

        return new TransactionReviewDocument
        {
            Path = relativePath,
            Operation = entry.Operation,
            OriginalExists = entry.OriginalExists,
            OriginalHash = entry.OriginalHash,
            IntendedHash = entry.IntendedHash,
            IntendedUnixFileMode = (int?)entry.IntendedUnixFileMode,
            Projects = CreateProjectOwners(session, entry.TargetPath),
            LineSummary = lineSummary,
        };
    }

    private TransactionReviewProject[] CreateProjectOwners(
        WorkspaceSessionSnapshot session,
        string targetPath)
    {
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("Review document projection requires an active transaction.");

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
