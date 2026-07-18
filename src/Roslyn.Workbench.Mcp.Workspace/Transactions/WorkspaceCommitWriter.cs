using System.Security.Cryptography;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitWriter : IWorkspaceCommitWriter
{
    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly ICommitRecoveryStore _recoveryStore;
    private readonly IAtomicFileCommitter _fileCommitter;

    public WorkspaceCommitWriter(
        IFileSystem fileSystem,
        IAtomicFileWriter atomicFileWriter,
        ICommitRecoveryStore recoveryStore,
        IAtomicFileCommitter fileCommitter)
    {
        _fileSystem = fileSystem;
        _atomicFileWriter = atomicFileWriter;
        _recoveryStore = recoveryStore;
        _fileCommitter = fileCommitter;
    }

    public async ValueTask<WorkspaceCommitValidationResult> RevalidateAsync(
        WorkspaceCommitManifest manifest,
        CancellationToken cancellationToken)
    {
        foreach (var entry in manifest.Entries)
        {
            var validation = await RevalidateEntryAsync(entry, cancellationToken);
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
            _fileSystem.Directory.CreateDirectory(directory);
        }

        foreach (var entry in manifest.Entries)
        {
            var validation = await RevalidateEntryAsync(entry, CancellationToken.None);
            if (!validation.IsValid)
            {
                return validation;
            }

            switch (entry.Operation)
            {
                case WorkspaceFileOperation.Create:
                case WorkspaceFileOperation.Replace:
                    var contents = await _recoveryStore.ReadArtifactAsync(manifest.CommitId, entry.GetRequiredStagedPath(), CancellationToken.None);
                    await _atomicFileWriter.WriteAllBytesAsync(entry.TargetPath, contents, CancellationToken.None);
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

    public ValueTask<bool> CompleteAsync(WorkspaceCommitManifest manifest)
    {
        try
        {
            foreach (var entry in manifest.Entries.Where(entry => entry.Operation == WorkspaceFileOperation.Delete))
            {
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
                    await _atomicFileWriter.WriteAllBytesAsync(entry.TargetPath, backup, CancellationToken.None);
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
        WorkspaceCommitEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
}
