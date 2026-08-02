using System.Security.Cryptography;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitWriter : IWorkspaceCommitWriter
{
    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IAtomicFileCommitter _fileCommitter;
    private readonly IPhysicalPathContainment _pathContainment;

    public WorkspaceCommitWriter(
        IFileSystem fileSystem,
        IAtomicFileWriter atomicFileWriter,
        ICommitRecoveryStore recoveryStore,
        IAtomicFileCommitter fileCommitter,
        IPhysicalPathContainment pathContainment)
    {
        _fileSystem = fileSystem;
        _atomicFileWriter = atomicFileWriter;
        _recoveryStore = recoveryStore;
        _fileCommitter = fileCommitter;
        _pathContainment = pathContainment;
    }

    public async ValueTask<WorkspaceCommitValidationResult> RevalidateAsync(
        WorkspaceCommitManifest manifest,
        CancellationToken cancellationToken)
    {
        foreach (var entry in manifest.Entries)
        {
            var validation = await RevalidateEntryAsync(manifest, entry, cancellationToken);
            if (!validation.IsValid)
            {
                return validation;
            }
        }

        return WorkspaceCommitValidationResult.Valid();
    }

    public async ValueTask<WorkspaceCommitValidationResult> ApplyAsync(WorkspaceCommitManifest manifest)
    {
        foreach (var directory in manifest.CreatedDirectories)
        {
            if (!_pathContainment.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                directory,
                out _))
            {
                return WorkspaceCommitValidationResult.Invalid(
                    $"The commit directory '{directory}' resolves outside the workspace root.");
            }

            _fileSystem.Directory.CreateDirectory(directory);
        }

        foreach (var entry in manifest.Entries)
        {
            var validation = await RevalidateEntryAsync(manifest, entry, CancellationToken.None);
            if (!validation.IsValid)
            {
                return validation;
            }

            switch (entry.Operation)
            {
                case WorkspaceFileOperation.Create:
                case WorkspaceFileOperation.Replace:
                    var contents = await _recoveryStore.ReadArtifactAsync(manifest.CommitId, entry.GetRequiredStagedPath(), CancellationToken.None);
                    await _atomicFileWriter.WriteAllBytesAsync(
                        entry.TargetPath,
                        contents,
                        AtomicFileAccess.Default,
                        CancellationToken.None);
                    break;

                case WorkspaceFileOperation.Delete:
                    if (_fileSystem.File.Exists(entry.GetRequiredDeleteMarkerPath()))
                    {
                        return WorkspaceCommitValidationResult.Invalid(
                            $"The delete marker '{entry.DeleteMarkerPath}' already exists.");
                    }

                    _fileCommitter.Move(entry.TargetPath, entry.GetRequiredDeleteMarkerPath());
                    break;
            }
        }

        return WorkspaceCommitValidationResult.Valid();
    }

    public async ValueTask<WorkspaceCommitValidationResult> ValidateAppliedStateAsync(
        WorkspaceCommitManifest manifest)
    {
        foreach (var entry in manifest.Entries)
        {
            var hasSafeRecoveryPaths = HasSafeRecoveryPaths(manifest, entry);
            if (!hasSafeRecoveryPaths)
            {
                return WorkspaceCommitValidationResult.Invalid(
                    $"The target '{entry.TargetPath}' resolves outside the workspace root.");
            }

            var targetExists = _fileSystem.File.Exists(entry.TargetPath);
            if (entry.Operation == WorkspaceFileOperation.Delete)
            {
                var deleteMarkerPath = entry.GetRequiredDeleteMarkerPath();
                var deleteMarkerExists = _fileSystem.File.Exists(deleteMarkerPath);
                var appliedDeleteIsValid = !targetExists && deleteMarkerExists;
                if (!appliedDeleteIsValid)
                {
                    return WorkspaceCommitValidationResult.Invalid(
                        $"The target '{entry.TargetPath}' changed after commit application.");
                }

                continue;
            }

            if (!targetExists)
            {
                return WorkspaceCommitValidationResult.Invalid(
                    $"The target '{entry.TargetPath}' changed after commit application.");
            }

            var targetHash = await HashFileAsync(entry.TargetPath, CancellationToken.None);
            var appliedContentsAreValid = string.Equals(
                targetHash,
                entry.IntendedHash,
                StringComparison.Ordinal);

            if (!appliedContentsAreValid)
            {
                return WorkspaceCommitValidationResult.Invalid(
                    $"The target '{entry.TargetPath}' changed after commit application.");
            }
        }

        return WorkspaceCommitValidationResult.Valid();
    }

    public ValueTask<bool> CompleteAsync(WorkspaceCommitManifest manifest)
    {
        try
        {
            foreach (var entry in manifest.Entries.Where(entry => entry.Operation == WorkspaceFileOperation.Delete))
            {
                if (!_pathContainment.TryGetStrictlyContainedPath(
                    manifest.WorkspaceRoot,
                    entry.GetRequiredDeleteMarkerPath(),
                    out _))
                {
                    return ValueTask.FromResult(false);
                }

                if (_fileSystem.File.Exists(entry.GetRequiredDeleteMarkerPath()))
                {
                    _fileSystem.File.Delete(entry.GetRequiredDeleteMarkerPath());
                }
            }

            return ValueTask.FromResult(true);
        }
        catch (IOException)
        {
            return ValueTask.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            return ValueTask.FromResult(false);
        }
    }

    public async ValueTask<RecoveryState> RestoreAsync(WorkspaceCommitManifest manifest)
    {
        var conflict = false;
        try
        {
            foreach (var entry in manifest.Entries.Reverse())
            {
                if (!HasSafeRecoveryPaths(manifest, entry))
                {
                    conflict = true;
                    continue;
                }

                var exists = _fileSystem.File.Exists(entry.TargetPath);
                var currentHash = exists ? await HashFileAsync(entry.TargetPath, CancellationToken.None) : null;
                if (entry.OriginalExists)
                {
                    if (entry.Operation == WorkspaceFileOperation.Delete)
                    {
                        var markerPath = entry.GetRequiredDeleteMarkerPath();
                        var markerExists = _fileSystem.File.Exists(markerPath);
                        if (markerExists && !exists)
                        {
                            _fileCommitter.Move(markerPath, entry.TargetPath);
                            continue;
                        }

                        if (markerExists && exists && string.Equals(currentHash, entry.OriginalHash, StringComparison.Ordinal))
                        {
                            _fileSystem.File.Delete(markerPath);
                            continue;
                        }

                        if (!markerExists && exists && string.Equals(currentHash, entry.OriginalHash, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        conflict = true;
                        continue;
                    }

                    if (exists && string.Equals(currentHash, entry.OriginalHash, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var isApplied = exists && string.Equals(currentHash, entry.IntendedHash, StringComparison.Ordinal);
                    if (!isApplied)
                    {
                        conflict = true;
                        continue;
                    }

                    var backup = await _recoveryStore.ReadArtifactAsync(manifest.CommitId, entry.GetRequiredBackupPath(), CancellationToken.None);
                    var targetDirectory = _fileSystem.Path.GetDirectoryName(entry.TargetPath)
                        ?? throw new InvalidOperationException($"The target '{entry.TargetPath}' does not have a parent directory.");

                    _fileSystem.Directory.CreateDirectory(targetDirectory);
                    await _atomicFileWriter.WriteAllBytesAsync(
                        entry.TargetPath,
                        backup,
                        AtomicFileAccess.Default,
                        CancellationToken.None);
                }
                else if (exists)
                {
                    if (string.Equals(currentHash, entry.IntendedHash, StringComparison.Ordinal))
                    {
                        _fileSystem.File.Delete(entry.TargetPath);
                    }
                    else
                    {
                        conflict = true;
                    }
                }
            }

            foreach (var directory in manifest.CreatedDirectories.OrderByDescending(path => path.Length))
            {
                if (!_pathContainment.TryGetStrictlyContainedPath(
                    manifest.WorkspaceRoot,
                    directory,
                    out _))
                {
                    conflict = true;
                    continue;
                }

                if (_fileSystem.Directory.Exists(directory) && !_fileSystem.Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    _fileSystem.Directory.Delete(directory);
                }
            }

            return conflict ? RecoveryState.RecoveryConflict : RecoveryState.Restored;
        }
        catch (IOException)
        {
            return RecoveryState.RecoveryIncomplete;
        }
        catch (UnauthorizedAccessException)
        {
            return RecoveryState.RecoveryIncomplete;
        }
    }

    private async ValueTask<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        var contents = await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken);
        return Convert.ToHexString(SHA256.HashData(contents));
    }

    private async ValueTask<WorkspaceCommitValidationResult> RevalidateEntryAsync(
        WorkspaceCommitManifest manifest,
        WorkspaceCommitEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasSafeRecoveryPaths(manifest, entry))
        {
            return WorkspaceCommitValidationResult.Invalid(
                $"The target '{entry.TargetPath}' resolves outside the workspace root.");
        }

        if (entry.Operation == WorkspaceFileOperation.Delete
            && _fileSystem.File.Exists(entry.GetRequiredDeleteMarkerPath()))
        {
            return WorkspaceCommitValidationResult.Invalid(
                $"The delete marker '{entry.DeleteMarkerPath}' already exists.");
        }

        if (_fileSystem.File.Exists(entry.TargetPath) != entry.OriginalExists)
        {
            return WorkspaceCommitValidationResult.Invalid(
                $"The target '{entry.TargetPath}' changed before commit application.");
        }

        if (entry.OriginalExists && !string.Equals(
            await HashFileAsync(entry.TargetPath, cancellationToken),
            entry.OriginalHash,
            StringComparison.Ordinal))
        {
            return WorkspaceCommitValidationResult.Invalid(
                $"The target '{entry.TargetPath}' changed before commit application.");
        }

        return WorkspaceCommitValidationResult.Valid();
    }

    private bool HasSafeRecoveryPaths(WorkspaceCommitManifest manifest, WorkspaceCommitEntry entry)
    {
        if (!_pathContainment.TryGetStrictlyContainedPath(
            manifest.WorkspaceRoot,
            entry.TargetPath,
            out _))
        {
            return false;
        }

        return entry.DeleteMarkerPath is null
            || _pathContainment.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                entry.DeleteMarkerPath,
                out _);
    }
}
