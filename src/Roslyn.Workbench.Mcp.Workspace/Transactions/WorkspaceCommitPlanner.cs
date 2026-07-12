using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitPlanner : IWorkspaceCommitPlanner
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;

    public WorkspaceCommitPlanner(IFileSystem fileSystem, IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
    }

    public async ValueTask<WorkspaceCommitPlan> CreateAsync(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = CreatePlanningContext(commitId, loadedPath, workspaceRoot, baselineSolution, currentSolution);
        var projectChanges = currentSolution.GetChanges(baselineSolution).GetProjectChanges();
        foreach (var projectChange in projectChanges)
        {
            await AddProjectChangesAsync(
                context,
                projectChange,
                baselineSolution,
                currentSolution,
                cancellationToken).ConfigureAwait(false);
        }

        return CreatePlan(context);
    }

    private WorkspaceCommitPlanningContext CreatePlanningContext(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        Solution baselineSolution,
        Solution currentSolution)
    {
        var comparer = _pathComparison.Comparer;
        return new WorkspaceCommitPlanningContext(
            commitId,
            _fileSystem.Path.GetFullPath(loadedPath),
            _fileSystem.Path.GetFullPath(workspaceRoot),
            GetProjectRoots(baselineSolution, currentSolution, comparer),
            GetBaselineDocumentPaths(baselineSolution, comparer),
            comparer);
    }

    private async ValueTask AddProjectChangesAsync(
        WorkspaceCommitPlanningContext context,
        ProjectChanges projectChanges,
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        var changedDocuments = projectChanges.GetChangedDocuments()
            .Select(currentSolution.GetDocument)
            .OfType<Document>();
        foreach (var document in changedDocuments)
        {
            await AddWriteAsync(
                context,
                document,
                WorkspaceFileOperation.Replace,
                cancellationToken).ConfigureAwait(false);
        }

        var addedDocuments = projectChanges.GetAddedDocuments()
            .Select(currentSolution.GetDocument)
            .OfType<Document>();
        foreach (var document in addedDocuments)
        {
            await AddWriteAsync(
                context,
                document,
                WorkspaceFileOperation.Create,
                cancellationToken).ConfigureAwait(false);
        }

        var removedDocuments = projectChanges.GetRemovedDocuments()
            .Select(baselineSolution.GetDocument)
            .OfType<Document>();
        foreach (var document in removedDocuments)
        {
            await AddDeleteAsync(context, document, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask AddWriteAsync(
        WorkspaceCommitPlanningContext context,
        Document document,
        WorkspaceFileOperation operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.FilePath is null)
        {
            return;
        }

        var path = ValidateTarget(context, document.FilePath);
        var originalExists = _fileSystem.File.Exists(path);
        if ((operation == WorkspaceFileOperation.Create) == originalExists)
        {
            throw new IOException($"The target '{path}' no longer has the expected existence state.");
        }

        var originalContents = originalExists
            ? await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
            : null;
        var intendedContents = await GetDocumentBytesAsync(document, cancellationToken).ConfigureAwait(false);
        var artifactIndex = GetArtifactIndex(context);
        var backupPath = originalExists ? $"backup/{artifactIndex}.bin" : null;
        var stagedPath = $"staged/{artifactIndex}.bin";

        if (backupPath is not null && originalContents is not null)
        {
            context.Artifacts.Add(backupPath, originalContents);
        }

        context.Artifacts.Add(stagedPath, intendedContents);
        AddMissingDirectories(context, path);
        context.Entries.Add(new WorkspaceCommitEntry
        {
            TargetPath = path,
            Operation = operation,
            OriginalExists = originalExists,
            OriginalHash = originalContents is null ? null : Hash(originalContents),
            IntendedHash = Hash(intendedContents),
            BackupPath = backupPath,
            StagedPath = stagedPath,
        });
    }

    private async ValueTask AddDeleteAsync(
        WorkspaceCommitPlanningContext context,
        Document document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.FilePath is null)
        {
            return;
        }

        var path = ValidateTarget(context, document.FilePath);
        if (!_fileSystem.File.Exists(path))
        {
            throw new IOException($"The target '{path}' no longer exists.");
        }

        var originalContents = await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var artifactIndex = GetArtifactIndex(context);
        var backupPath = $"backup/{artifactIndex}.bin";
        var deleteMarkerPath = $"{path}.{context.CommitId}.delete";
        if (_fileSystem.File.Exists(deleteMarkerPath))
        {
            throw new IOException($"The delete marker '{deleteMarkerPath}' already exists.");
        }

        context.Artifacts.Add(backupPath, originalContents);
        context.Entries.Add(new WorkspaceCommitEntry
        {
            TargetPath = path,
            Operation = WorkspaceFileOperation.Delete,
            OriginalExists = true,
            OriginalHash = Hash(originalContents),
            BackupPath = backupPath,
            DeleteMarkerPath = deleteMarkerPath,
        });
    }

    private string ValidateTarget(WorkspaceCommitPlanningContext context, string path)
    {
        var canonicalPath = _fileSystem.Path.GetFullPath(path);
        if (!context.Targets.Add(canonicalPath))
        {
            throw new InvalidOperationException($"The commit contains the duplicate target '{canonicalPath}'.");
        }

        if (!IsWithinBoundary(context.WorkspaceRoot, canonicalPath))
        {
            throw new InvalidOperationException($"The target '{canonicalPath}' is outside the workspace root.");
        }

        var isSupported = context.BaselineDocumentPaths.Contains(canonicalPath)
            || context.ProjectRoots.Any(projectRoot => IsWithinBoundary(projectRoot, canonicalPath));
        if (!isSupported)
        {
            throw new InvalidOperationException($"The target '{canonicalPath}' is outside the loaded project boundaries.");
        }

        return canonicalPath;
    }

    private void AddMissingDirectories(WorkspaceCommitPlanningContext context, string path)
    {
        var directory = _fileSystem.Path.GetDirectoryName(path);
        while (directory is not null && !_fileSystem.Directory.Exists(directory))
        {
            context.CreatedDirectories.Add(directory);
            directory = _fileSystem.Path.GetDirectoryName(directory);
        }
    }

    private bool IsWithinBoundary(string root, string path)
    {
        var relativePath = _fileSystem.Path.GetRelativePath(root, path);
        return relativePath != ".."
            && !relativePath.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !_fileSystem.Path.IsPathRooted(relativePath);
    }

    private IReadOnlyList<string> GetProjectRoots(
        Solution baselineSolution,
        Solution currentSolution,
        IEqualityComparer<string> comparer)
    {
        return baselineSolution.Projects
            .Concat(currentSolution.Projects)
            .Select(project => project.FilePath)
            .OfType<string>()
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(GetProjectRoot)
            .Distinct(comparer)
            .ToArray();
    }

    private HashSet<string> GetBaselineDocumentPaths(
        Solution baselineSolution,
        IEqualityComparer<string> comparer)
    {
        return baselineSolution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => document.FilePath)
            .OfType<string>()
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(_fileSystem.Path.GetFullPath)
            .ToHashSet(comparer);
    }

    private string GetProjectRoot(string projectPath)
    {
        var canonicalProjectPath = _fileSystem.Path.GetFullPath(projectPath);
        return _fileSystem.Path.GetDirectoryName(canonicalProjectPath)
            ?? throw new InvalidOperationException($"The project path '{projectPath}' does not have a parent directory.");
    }

    private static WorkspaceCommitPlan CreatePlan(WorkspaceCommitPlanningContext context)
    {
        return new WorkspaceCommitPlan(
            new WorkspaceCommitManifest
            {
                CommitId = context.CommitId,
                LoadedPath = context.LoadedPath,
                WorkspaceRoot = context.WorkspaceRoot,
                State = Contracts.Results.RecoveryState.Prepared,
                Entries = context.Entries,
                CreatedDirectories = context.CreatedDirectories.OrderBy(path => path.Length).ToArray(),
            },
            context.Artifacts);
    }

    private static async ValueTask<byte[]> GetDocumentBytesAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var encoding = text.Encoding ?? Encoding.UTF8;
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text.ToString())];
    }

    private static string GetArtifactIndex(WorkspaceCommitPlanningContext context)
    {
        return context.Entries.Count.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Hash(ReadOnlySpan<byte> contents)
    {
        return Convert.ToHexString(SHA256.HashData(contents));
    }
}
