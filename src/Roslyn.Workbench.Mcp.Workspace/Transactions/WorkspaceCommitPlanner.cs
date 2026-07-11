using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitPlanner : IWorkspaceCommitPlanner
{
    private readonly IFileSystem _fileSystem;

    public WorkspaceCommitPlanner(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
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
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var canonicalWorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot);
        var targets = new HashSet<string>(comparer);
        var entries = new List<WorkspaceCommitEntry>();
        var artifacts = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        var createdDirectories = new HashSet<string>(comparer);
        var projectRoots = baselineSolution.Projects.Concat(currentSolution.Projects)
            .Select(project => project.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => _fileSystem.Path.GetDirectoryName(_fileSystem.Path.GetFullPath(path!))!)
            .Distinct(comparer)
            .ToArray();
        var baselineDocumentPaths = baselineSolution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => document.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => _fileSystem.Path.GetFullPath(path!))
            .ToHashSet(comparer);

        foreach (var change in currentSolution.GetChanges(baselineSolution).GetProjectChanges())
        {
            foreach (var documentId in change.GetChangedDocuments())
            {
                await AddWriteAsync(currentSolution.GetDocument(documentId), WorkspaceFileOperation.Replace);
            }

            foreach (var documentId in change.GetAddedDocuments())
            {
                await AddWriteAsync(currentSolution.GetDocument(documentId), WorkspaceFileOperation.Create);
            }

            foreach (var documentId in change.GetRemovedDocuments())
            {
                await AddDeleteAsync(baselineSolution.GetDocument(documentId));
            }
        }

        return new WorkspaceCommitPlan(
            new WorkspaceCommitManifest
            {
                CommitId = commitId,
                LoadedPath = _fileSystem.Path.GetFullPath(loadedPath),
                WorkspaceRoot = _fileSystem.Path.GetFullPath(workspaceRoot),
                State = Contracts.Results.RecoveryState.Prepared,
                Entries = entries,
                CreatedDirectories = createdDirectories.OrderBy(path => path.Length).ToArray(),
            },
            artifacts);

        async ValueTask AddWriteAsync(Document? document, WorkspaceFileOperation operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document?.FilePath is null)
            {
                return;
            }

            var path = ValidateTarget(document.FilePath);
            var originalExists = _fileSystem.File.Exists(path);
            if ((operation == WorkspaceFileOperation.Create) == originalExists)
            {
                throw new IOException($"The target '{path}' no longer has the expected existence state.");
            }

            var original = originalExists
                ? await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                : null;
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var encoding = text.Encoding ?? Encoding.UTF8;
            var preamble = encoding.GetPreamble();
            var encodedText = encoding.GetBytes(text.ToString());
            var intended = new byte[preamble.Length + encodedText.Length];
            preamble.CopyTo(intended, 0);
            encodedText.CopyTo(intended, preamble.Length);
            var index = entries.Count.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            var backup = originalExists ? $"backup/{index}.bin" : null;
            var staged = $"staged/{index}.bin";
            if (backup is not null)
            {
                artifacts.Add(backup, original!);
            }

            artifacts.Add(staged, intended);
            AddMissingDirectories(path);
            entries.Add(new WorkspaceCommitEntry
            {
                TargetPath = path,
                Operation = operation,
                OriginalExists = originalExists,
                OriginalHash = original is null ? null : Hash(original),
                IntendedHash = Hash(intended),
                BackupPath = backup,
                StagedPath = staged,
            });
        }

        async ValueTask AddDeleteAsync(Document? document)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document?.FilePath is null)
            {
                return;
            }

            var path = ValidateTarget(document.FilePath);
            if (!_fileSystem.File.Exists(path))
            {
                throw new IOException($"The target '{path}' no longer exists.");
            }

            var original = await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var index = entries.Count.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            var backup = $"backup/{index}.bin";
            var deleteMarker = $"{path}.{commitId}.delete";
            if (_fileSystem.File.Exists(deleteMarker))
            {
                throw new IOException($"The delete marker '{deleteMarker}' already exists.");
            }

            artifacts.Add(backup, original);
            entries.Add(new WorkspaceCommitEntry
            {
                TargetPath = path,
                Operation = WorkspaceFileOperation.Delete,
                OriginalExists = true,
                OriginalHash = Hash(original),
                BackupPath = backup,
                DeleteMarkerPath = deleteMarker,
            });
        }

        string ValidateTarget(string path)
        {
            var canonical = _fileSystem.Path.GetFullPath(path);
            if (!targets.Add(canonical))
            {
                throw new InvalidOperationException($"The commit contains the duplicate target '{canonical}'.");
            }

            var workspaceRelative = _fileSystem.Path.GetRelativePath(canonicalWorkspaceRoot, canonical);
            if (workspaceRelative == ".."
                || workspaceRelative.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || _fileSystem.Path.IsPathRooted(workspaceRelative))
            {
                throw new InvalidOperationException($"The target '{canonical}' is outside the workspace root.");
            }

            var supported = baselineDocumentPaths.Contains(canonical) || projectRoots.Any(root =>
            {
                var relative = _fileSystem.Path.GetRelativePath(root, canonical);
                return relative != ".." && !relative.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !_fileSystem.Path.IsPathRooted(relative);
            });
            if (!supported)
            {
                throw new InvalidOperationException($"The target '{canonical}' is outside the loaded project boundaries.");
            }

            return canonical;
        }

        void AddMissingDirectories(string path)
        {
            var directory = _fileSystem.Path.GetDirectoryName(path);
            while (directory is not null && !_fileSystem.Directory.Exists(directory))
            {
                createdDirectories.Add(directory);
                directory = _fileSystem.Path.GetDirectoryName(directory);
            }
        }
    }

    private static string Hash(ReadOnlySpan<byte> contents)
    {
        return Convert.ToHexString(SHA256.HashData(contents));
    }
}
