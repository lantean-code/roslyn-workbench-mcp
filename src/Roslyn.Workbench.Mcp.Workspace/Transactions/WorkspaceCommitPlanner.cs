using System.Diagnostics.CodeAnalysis;
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

    public async ValueTask<WorkspaceCommitPlanResult> CreateAsync(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryCreatePlanningContext(
            commitId,
            loadedPath,
            workspaceRoot,
            baselineSolution,
            currentSolution,
            out var context,
            out var contextError))
        {
            return WorkspaceCommitPlanResult.Failed(contextError);
        }

        var projectChanges = currentSolution.GetChanges(baselineSolution).GetProjectChanges();
        foreach (var projectChange in projectChanges)
        {
            var validation = await AddProjectChangesAsync(
                context,
                projectChange,
                baselineSolution,
                currentSolution,
                cancellationToken);
            if (!validation.IsValid)
            {
                return WorkspaceCommitPlanResult.Failed(validation.ErrorMessage);
            }
        }

        return WorkspaceCommitPlanResult.Succeeded(CreatePlan(context));
    }

    private bool TryCreatePlanningContext(
        string commitId,
        string loadedPath,
        string workspaceRoot,
        Solution baselineSolution,
        Solution currentSolution,
        [NotNullWhen(true)] out WorkspaceCommitPlanningContext? context,
        [NotNullWhen(false)] out string? errorMessage)
    {
        var comparer = _pathComparison.GetComparer(workspaceRoot);
        if (!TryGetProjectRoots(baselineSolution, currentSolution, comparer, out var projectRoots, out errorMessage))
        {
            context = null;
            return false;
        }

        context = new WorkspaceCommitPlanningContext(
            commitId,
            _fileSystem.Path.GetFullPath(loadedPath),
            _fileSystem.Path.GetFullPath(workspaceRoot),
            projectRoots,
            GetBaselineDocumentPaths(baselineSolution, comparer),
            comparer);

        errorMessage = null;
        return true;
    }

    private async ValueTask<WorkspaceCommitValidationResult> AddProjectChangesAsync(
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
            var validation = await AddWriteAsync(
                context,
                document,
                WorkspaceFileOperation.Replace,
                cancellationToken);
            if (!validation.IsValid)
            {
                return validation;
            }
        }

        var addedDocuments = projectChanges.GetAddedDocuments()
            .Select(currentSolution.GetDocument)
            .OfType<Document>();
        foreach (var document in addedDocuments)
        {
            var validation = await AddWriteAsync(
                context,
                document,
                WorkspaceFileOperation.Create,
                cancellationToken);
            if (!validation.IsValid)
            {
                return validation;
            }
        }

        var removedDocuments = projectChanges.GetRemovedDocuments()
            .Select(baselineSolution.GetDocument)
            .OfType<Document>();
        foreach (var document in removedDocuments)
        {
            var validation = await AddDeleteAsync(context, document, cancellationToken);
            if (!validation.IsValid)
            {
                return validation;
            }
        }

        return WorkspaceCommitValidationResult.Valid();
    }

    private async ValueTask<WorkspaceCommitValidationResult> AddWriteAsync(
        WorkspaceCommitPlanningContext context,
        Document document,
        WorkspaceFileOperation operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.FilePath is null)
        {
            return WorkspaceCommitValidationResult.Valid();
        }

        if (!TryValidateTarget(context, document.FilePath, out var path, out var targetError))
        {
            return WorkspaceCommitValidationResult.Invalid(targetError);
        }

        var intendedContents = await GetDocumentBytesAsync(document, cancellationToken);
        var intendedHash = Hash(intendedContents);
        if (context.EntriesByTarget.TryGetValue(path, out var existingEntry))
        {
            return existingEntry.Operation == operation
                && string.Equals(existingEntry.IntendedHash, intendedHash, StringComparison.Ordinal)
                    ? WorkspaceCommitValidationResult.Valid()
                    : WorkspaceCommitValidationResult.Invalid(
                        $"The commit contains conflicting changes for the duplicate target '{path}'.");
        }

        var originalExists = _fileSystem.File.Exists(path);
        if ((operation == WorkspaceFileOperation.Create) == originalExists)
        {
            return WorkspaceCommitValidationResult.Invalid(
                $"The target '{path}' no longer has the expected existence state.");
        }

        var originalContents = originalExists
            ? await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken)
            : null;
        var artifactIndex = GetArtifactIndex(context);
        var backupPath = originalExists ? $"backup/{artifactIndex}.bin" : null;
        var stagedPath = $"staged/{artifactIndex}.bin";

        if (backupPath is not null && originalContents is not null)
        {
            context.Artifacts.Add(backupPath, originalContents);
        }

        context.Artifacts.Add(stagedPath, intendedContents);
        AddMissingDirectories(context, path);
        var entry = new WorkspaceCommitEntry
        {
            TargetPath = path,
            Operation = operation,
            OriginalExists = originalExists,
            OriginalHash = originalContents is null ? null : Hash(originalContents),
            IntendedHash = intendedHash,
            BackupPath = backupPath,
            StagedPath = stagedPath,
        };

        context.Entries.Add(entry);
        context.EntriesByTarget.Add(path, entry);
        return WorkspaceCommitValidationResult.Valid();
    }

    private async ValueTask<WorkspaceCommitValidationResult> AddDeleteAsync(
        WorkspaceCommitPlanningContext context,
        Document document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.FilePath is null)
        {
            return WorkspaceCommitValidationResult.Valid();
        }

        if (!TryValidateTarget(context, document.FilePath, out var path, out var targetError))
        {
            return WorkspaceCommitValidationResult.Invalid(targetError);
        }

        if (context.EntriesByTarget.TryGetValue(path, out var existingEntry))
        {
            return existingEntry.Operation == WorkspaceFileOperation.Delete
                ? WorkspaceCommitValidationResult.Valid()
                : WorkspaceCommitValidationResult.Invalid(
                    $"The commit contains conflicting changes for the duplicate target '{path}'.");
        }

        if (!_fileSystem.File.Exists(path))
        {
            return WorkspaceCommitValidationResult.Invalid($"The target '{path}' no longer exists.");
        }

        var originalContents = await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken);
        var artifactIndex = GetArtifactIndex(context);
        var backupPath = $"backup/{artifactIndex}.bin";
        var deleteMarkerPath = $"{path}.{context.CommitId}.delete";
        if (_fileSystem.File.Exists(deleteMarkerPath))
        {
            return WorkspaceCommitValidationResult.Invalid(
                $"The delete marker '{deleteMarkerPath}' already exists.");
        }

        context.Artifacts.Add(backupPath, originalContents);
        var entry = new WorkspaceCommitEntry
        {
            TargetPath = path,
            Operation = WorkspaceFileOperation.Delete,
            OriginalExists = true,
            OriginalHash = Hash(originalContents),
            BackupPath = backupPath,
            DeleteMarkerPath = deleteMarkerPath,
        };

        context.Entries.Add(entry);
        context.EntriesByTarget.Add(path, entry);
        return WorkspaceCommitValidationResult.Valid();
    }

    private bool TryValidateTarget(
        WorkspaceCommitPlanningContext context,
        string path,
        [NotNullWhen(true)] out string? canonicalPath,
        [NotNullWhen(false)] out string? errorMessage)
    {
        canonicalPath = null;
        var targetPath = _fileSystem.Path.GetFullPath(path);
        if (context.EntriesByTarget.Comparer.Equals(context.WorkspaceRoot, targetPath)
            || !IsWithinBoundary(context.WorkspaceRoot, targetPath))
        {
            errorMessage = $"The target '{targetPath}' is outside the workspace root.";
            return false;
        }

        var isSupported = context.BaselineDocumentPaths.Contains(targetPath);
        foreach (var projectRoot in context.ProjectRoots)
        {
            if (IsWithinBoundary(projectRoot, targetPath))
            {
                isSupported = true;
                break;
            }
        }

        if (!isSupported)
        {
            errorMessage = $"The target '{targetPath}' is outside the loaded project boundaries.";
            return false;
        }

        canonicalPath = targetPath;
        errorMessage = null;
        return true;
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

    private bool TryGetProjectRoots(
        Solution baselineSolution,
        Solution currentSolution,
        IEqualityComparer<string> comparer,
        [NotNullWhen(true)] out string[]? projectRoots,
        [NotNullWhen(false)] out string? errorMessage)
    {
        var roots = new HashSet<string>(comparer);
        foreach (var project in baselineSolution.Projects.Concat(currentSolution.Projects))
        {
            if (string.IsNullOrWhiteSpace(project.FilePath))
            {
                continue;
            }

            var canonicalProjectPath = _fileSystem.Path.GetFullPath(project.FilePath);
            var projectRoot = _fileSystem.Path.GetDirectoryName(canonicalProjectPath);
            if (projectRoot is null)
            {
                projectRoots = null;
                errorMessage = $"The project path '{project.FilePath}' does not have a parent directory.";
                return false;
            }

            roots.Add(projectRoot);
        }

        projectRoots = roots.ToArray();
        errorMessage = null;
        return true;
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
        var text = await document.GetTextAsync(cancellationToken);
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
