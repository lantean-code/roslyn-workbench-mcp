namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Inspects changed Roslyn documents through the shared generated-source classifier.
/// </summary>
internal sealed class GeneratedSourceMutationInspector : IGeneratedSourceMutationInspector
{
    private readonly IGeneratedSourceClassifier _classifier;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedSourceMutationInspector"/> class.
    /// </summary>
    /// <param name="classifier">The shared generated-source classifier.</param>
    /// <param name="pathComparison">The platform-aware path comparison service.</param>
    /// <param name="fileSystem">The file-system abstraction used for relative path calculations.</param>
    public GeneratedSourceMutationInspector(
        IGeneratedSourceClassifier classifier,
        IWorkspacePathComparison pathComparison,
        IFileSystem fileSystem)
    {
        _classifier = classifier;
        _pathComparison = pathComparison;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>> FindGeneratedSourcePathsAsync(
        Solution baselineSolution,
        Solution candidateSolution,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var inspectedPaths = new HashSet<FileSystemPathKey>();
        var generatedPaths = new List<string>();
        var solutionChanges = candidateSolution.GetChanges(baselineSolution);
        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            foreach (var document in GetChangedDocuments(projectChanges, baselineSolution, candidateSolution))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (document.FilePath is null)
                {
                    continue;
                }

                var pathKey = _pathComparison.CreateKey(document.FilePath);
                if (!inspectedPaths.Add(pathKey))
                {
                    continue;
                }

                var isGeneratedLooking = await _classifier.IsGeneratedLookingAsync(
                    document,
                    workspaceRoot,
                    cancellationToken);

                if (isGeneratedLooking)
                {
                    generatedPaths.Add(CreateRelativePath(workspaceRoot, document.FilePath));
                }
            }
        }

        generatedPaths.Sort(StringComparer.Ordinal);
        return generatedPaths;
    }

    private static IEnumerable<Document> GetChangedDocuments(
        ProjectChanges projectChanges,
        Solution baselineSolution,
        Solution candidateSolution)
    {
        foreach (var documentId in projectChanges.GetAddedDocuments().Concat(projectChanges.GetChangedDocuments()))
        {
            var document = candidateSolution.GetDocument(documentId);
            if (document is not null)
            {
                yield return document;
            }
        }

        foreach (var documentId in projectChanges.GetRemovedDocuments())
        {
            var document = baselineSolution.GetDocument(documentId);
            if (document is not null)
            {
                yield return document;
            }
        }
    }

    private string CreateRelativePath(string workspaceRoot, string path)
    {
        return _fileSystem.Path
            .GetRelativePath(workspaceRoot, path)
            .Replace('\\', '/');
    }
}
