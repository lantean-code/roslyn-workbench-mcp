namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Compares loaded external document text with disk while deduplicating reads of linked files.
/// </summary>
internal sealed class WorkspaceReadOnlyDocumentValidator : IWorkspaceReadOnlyDocumentValidator
{
    private readonly IFileSystem _fileSystem;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceReadOnlyDocumentValidator"/> class.
    /// </summary>
    /// <param name="fileSystem">The filesystem abstraction used to read external documents.</param>
    /// <param name="pathContainment">The physical containment service used to distinguish writable documents.</param>
    /// <param name="pathComparison">The platform-aware path comparer used to deduplicate linked files.</param>
    public WorkspaceReadOnlyDocumentValidator(
        IFileSystem fileSystem,
        IPhysicalPathContainment pathContainment,
        IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathContainment = pathContainment;
        _pathComparison = pathComparison;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceReadOnlyDocumentValidationStatus> ValidateAsync(
        Solution solution,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diskTextByPath = new Dictionary<FileSystemPathKey, SourceText>();
        foreach (var project in solution.Projects)
        {
            var documents = project.Documents
                .Cast<TextDocument>()
                .Concat(project.AdditionalDocuments)
                .Concat(project.AnalyzerConfigDocuments);

            foreach (var document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(document.FilePath)
                    || _pathContainment.TryGetContainedPath(workspaceRoot, document.FilePath, out _))
                {
                    continue;
                }

                var documentText = await document.GetTextAsync(cancellationToken);
                var diskText = GetDiskTextOrDefault(
                    document.FilePath,
                    documentText,
                    diskTextByPath);

                if (diskText is null)
                {
                    return WorkspaceReadOnlyDocumentValidationStatus.Invalid;
                }

                var matchesDisk = documentText.ContentEquals(diskText);
                if (!matchesDisk)
                {
                    return WorkspaceReadOnlyDocumentValidationStatus.Invalid;
                }
            }
        }

        return WorkspaceReadOnlyDocumentValidationStatus.Valid;
    }

    private SourceText? GetDiskTextOrDefault(
        string path,
        SourceText documentText,
        Dictionary<FileSystemPathKey, SourceText> diskTextByPath)
    {
        var pathKey = _pathComparison.CreateKey(path);
        if (diskTextByPath.TryGetValue(pathKey, out var diskText))
        {
            return diskText;
        }

        try
        {
            using var stream = _fileSystem.File.OpenRead(path);
            diskText = SourceText.From(
                stream,
                documentText.Encoding,
                documentText.ChecksumAlgorithm);

            diskTextByPath.Add(pathKey, diskText);
            return diskText;
        }
        catch (Exception exception) when (IsExpectedFileFailure(exception))
        {
            return null;
        }
    }

    private static bool IsExpectedFileFailure(Exception exception)
    {
        return exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;
    }
}
