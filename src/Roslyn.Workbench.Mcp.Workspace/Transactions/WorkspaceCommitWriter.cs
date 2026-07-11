using System.Security.Cryptography;

using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

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

    public async ValueTask RevalidateAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken)
    {
        foreach (var entry in manifest.Entries)
        {
            await RevalidateEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask ApplyAsync(WorkspaceCommitManifest manifest)
    {
        foreach (var directory in manifest.CreatedDirectories)
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        foreach (var entry in manifest.Entries)
        {
            await RevalidateEntryAsync(entry, CancellationToken.None).ConfigureAwait(false);
            switch (entry.Operation)
            {
                case WorkspaceFileOperation.Create:
                case WorkspaceFileOperation.Replace:
                    var contents = await _recoveryStore.ReadArtifactAsync(manifest.CommitId, entry.StagedPath!, CancellationToken.None).ConfigureAwait(false);
                    await _atomicFileWriter.WriteAllBytesAsync(entry.TargetPath, contents, CancellationToken.None).ConfigureAwait(false);
                    break;
                case WorkspaceFileOperation.Delete:
                    if (_fileSystem.File.Exists(entry.DeleteMarkerPath!))
                    {
                        throw new IOException($"The delete marker '{entry.DeleteMarkerPath}' already exists.");
                    }

                    _fileCommitter.Move(entry.TargetPath, entry.DeleteMarkerPath!);
                    break;
            }
        }
    }

    public ValueTask<bool> CompleteAsync(WorkspaceCommitManifest manifest)
    {
        try
        {
            foreach (var entry in manifest.Entries.Where(entry => entry.Operation == WorkspaceFileOperation.Delete))
            {
                if (_fileSystem.File.Exists(entry.DeleteMarkerPath!))
                {
                    _fileSystem.File.Delete(entry.DeleteMarkerPath!);
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
                var currentHash = exists ? await HashFileAsync(entry.TargetPath, CancellationToken.None).ConfigureAwait(false) : null;
                if (entry.OriginalExists)
                {
                    if (entry.Operation == WorkspaceFileOperation.Delete)
                    {
                        var markerExists = _fileSystem.File.Exists(entry.DeleteMarkerPath!);
                        if (markerExists && !exists)
                        {
                            _fileCommitter.Move(entry.DeleteMarkerPath!, entry.TargetPath);
                            continue;
                        }

                        if (markerExists && exists && string.Equals(currentHash, entry.OriginalHash, StringComparison.Ordinal))
                        {
                            _fileSystem.File.Delete(entry.DeleteMarkerPath!);
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

                    var backup = await _recoveryStore.ReadArtifactAsync(manifest.CommitId, entry.BackupPath!, CancellationToken.None).ConfigureAwait(false);
                    _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(entry.TargetPath)!);
                    await _atomicFileWriter.WriteAllBytesAsync(entry.TargetPath, backup, CancellationToken.None).ConfigureAwait(false);
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
        var contents = await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(SHA256.HashData(contents));
    }

    private async ValueTask RevalidateEntryAsync(WorkspaceCommitEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entry.Operation == WorkspaceFileOperation.Delete && _fileSystem.File.Exists(entry.DeleteMarkerPath!))
        {
            throw new IOException($"The delete marker '{entry.DeleteMarkerPath}' already exists.");
        }

        if (_fileSystem.File.Exists(entry.TargetPath) != entry.OriginalExists)
        {
            throw new IOException($"The target '{entry.TargetPath}' changed before commit application.");
        }

        if (entry.OriginalExists && !string.Equals(
            await HashFileAsync(entry.TargetPath, cancellationToken).ConfigureAwait(false),
            entry.OriginalHash,
            StringComparison.Ordinal))
        {
            throw new IOException($"The target '{entry.TargetPath}' changed before commit application.");
        }
    }
}
