using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed class CommitRecoveryStore : ICommitRecoveryStore
{
    private const string _manifestFileName = "manifest.json";
    private const string _ownerFileName = "owner.json";
    private const string _recoveryDirectoryName = "recovery";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Encoding _encoding = new UTF8Encoding(false);
    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly string _recoveryDirectory;

    public CommitRecoveryStore(IOptions<WorkspaceCoordinatorOptions> options, IFileSystem fileSystem, IAtomicFileWriter atomicFileWriter)
    {
        _fileSystem = fileSystem;
        _atomicFileWriter = atomicFileWriter;
        _recoveryDirectory = _fileSystem.Path.Combine(
            _fileSystem.Path.GetFullPath(options.Value.StateDirectory),
            _recoveryDirectoryName);
    }

    public async ValueTask PersistPlanAsync(WorkspaceCommitPlan plan, CancellationToken cancellationToken)
    {
        _fileSystem.Directory.CreateDirectory(GetCommitDirectory(plan.Manifest.CommitId));
        await WriteOwnerAsync(plan.Manifest, cancellationToken).ConfigureAwait(false);
        await WriteArtifactsAsync(plan, cancellationToken).ConfigureAwait(false);
        await WriteManifestAsync(plan.Manifest, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask WriteManifestAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _fileSystem.Directory.CreateDirectory(GetCommitDirectory(manifest.CommitId));
        return WriteJsonAsync(GetManifestPath(manifest.CommitId), manifest, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<WorkspaceCommitManifest>> GetManifestsAsync(CancellationToken cancellationToken)
    {
        var manifests = new List<WorkspaceCommitManifest>();
        if (!_fileSystem.Directory.Exists(_recoveryDirectory))
        {
            return manifests;
        }

        foreach (var directory in _fileSystem.Directory.EnumerateDirectories(_recoveryDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetManifestPathInDirectory(directory);
            if (!_fileSystem.File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var manifest = JsonSerializer.Deserialize<WorkspaceCommitManifest>(json, _serializerOptions);
                if (manifest is not null && IsValidManifest(manifest, directory))
                {
                    manifests.Add(manifest);
                }
                else
                {
                    manifests.Add(CreateInvalidManifest(directory, manifest?.LoadedPath, manifest?.WorkspaceRoot));
                }
            }
            catch (IOException)
            {
                manifests.Add(CreateInvalidManifest(directory, loadedPath: null, workspaceRoot: null));
            }
            catch (UnauthorizedAccessException)
            {
                manifests.Add(CreateInvalidManifest(directory, loadedPath: null, workspaceRoot: null));
            }
            catch (JsonException)
            {
                manifests.Add(CreateInvalidManifest(directory, loadedPath: null, workspaceRoot: null));
            }
        }

        return manifests;
    }

    public async ValueTask<IReadOnlyList<WorkspaceCommitOwner>> GetOrphanedCommitOwnersAsync(CancellationToken cancellationToken)
    {
        var owners = new List<WorkspaceCommitOwner>();
        if (!_fileSystem.Directory.Exists(_recoveryDirectory))
        {
            return owners;
        }

        foreach (var directory in _fileSystem.Directory.EnumerateDirectories(_recoveryDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_fileSystem.File.Exists(GetManifestPathInDirectory(directory)))
            {
                continue;
            }

            var ownerPath = GetOwnerPathInDirectory(directory);
            if (!_fileSystem.File.Exists(ownerPath))
            {
                continue;
            }

            try
            {
                var json = await _fileSystem.File.ReadAllTextAsync(ownerPath, cancellationToken).ConfigureAwait(false);
                var owner = JsonSerializer.Deserialize<WorkspaceCommitOwner>(json, _serializerOptions);
                if (owner is not null
                    && owner.Version == 2
                    && string.Equals(
                        owner.CommitId,
                        _fileSystem.Path.GetFileName(directory),
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                    && _fileSystem.Path.IsPathFullyQualified(owner.LoadedPath)
                    && _fileSystem.Path.IsPathFullyQualified(owner.WorkspaceRoot))
                {
                    owners.Add(owner);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
        }

        return owners;
    }

    public async ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        var statuses = (await GetManifestsAsync(cancellationToken).ConfigureAwait(false))
            .Select(manifest => new RecoveryStatus
            {
                CommitId = manifest.CommitId,
                SolutionPath = manifest.LoadedPath,
                WorkspaceRoot = manifest.WorkspaceRoot,
                State = manifest.State,
                Message = manifest.Message,
            }).ToList();

        statuses.AddRange((await GetOrphanedCommitOwnersAsync(cancellationToken).ConfigureAwait(false)).Select(owner => new RecoveryStatus
        {
            CommitId = owner.CommitId,
            SolutionPath = owner.LoadedPath,
            WorkspaceRoot = owner.WorkspaceRoot,
            State = RecoveryState.RecoveryConflict,
            Message = "The commit was interrupted before its durable manifest was prepared.",
        }));

        if (!_fileSystem.Directory.Exists(_recoveryDirectory))
        {
            return statuses;
        }

        foreach (var path in _fileSystem.Directory.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var legacy = JsonSerializer.Deserialize<RecoveryStatus>(json, _serializerOptions);
                if (legacy is not null)
                {
                    statuses.Add(legacy with { State = RecoveryState.RecoveryConflict, Message = "Legacy recovery evidence cannot be restored automatically." });
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
        }

        return statuses;
    }

    public ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetLegacyStatusPath(status.CommitId);
        _fileSystem.Directory.CreateDirectory(_recoveryDirectory);
        return WriteJsonAsync(path, status, cancellationToken);
    }

    public async ValueTask<byte[]> ReadArtifactAsync(string commitId, string relativePath, CancellationToken cancellationToken)
    {
        return await _fileSystem.File.ReadAllBytesAsync(GetArtifactPath(commitId, relativePath), cancellationToken).ConfigureAwait(false);
    }

    public void DeleteStatus(string commitId)
    {
        var directory = GetCommitDirectory(commitId);
        if (_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.Delete(directory, recursive: true);
        }

        var legacy = GetLegacyStatusPath(commitId);
        if (_fileSystem.File.Exists(legacy))
        {
            _fileSystem.File.Delete(legacy);
        }
    }

    private string GetCommitDirectory(string commitId)
    {
        ValidateCommitId(commitId);
        return _fileSystem.Path.Combine(_recoveryDirectory, commitId);
    }

    private string GetLegacyStatusPath(string commitId)
    {
        ValidateCommitId(commitId);
        return _fileSystem.Path.Combine(_recoveryDirectory, $"{commitId}.json");
    }

    private string GetManifestPath(string commitId)
    {
        return _fileSystem.Path.Combine(GetCommitDirectory(commitId), _manifestFileName);
    }

    private string GetManifestPathInDirectory(string directory)
    {
        return _fileSystem.Path.Combine(directory, _manifestFileName);
    }

    private string GetOwnerPath(string commitId)
    {
        return _fileSystem.Path.Combine(GetCommitDirectory(commitId), _ownerFileName);
    }

    private string GetOwnerPathInDirectory(string directory)
    {
        return _fileSystem.Path.Combine(directory, _ownerFileName);
    }

    private string GetArtifactPath(string commitId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var root = GetCommitDirectory(commitId);
        var path = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(root, relativePath));
        var relative = _fileSystem.Path.GetRelativePath(root, path);

        if (relative == ".." || relative.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", StringComparison.Ordinal) || _fileSystem.Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("The recovery artifact path escapes its commit directory.");
        }

        return path;
    }

    private bool IsValidManifest(WorkspaceCommitManifest manifest, string directory)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (manifest.Version != 2
            || !string.Equals(manifest.CommitId, _fileSystem.Path.GetFileName(directory), comparison)
            || !_fileSystem.Path.IsPathFullyQualified(manifest.LoadedPath)
            || !_fileSystem.Path.IsPathFullyQualified(manifest.WorkspaceRoot)
            || !IsWithinRoot(manifest.WorkspaceRoot, manifest.LoadedPath))
        {
            return false;
        }

        var targets = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        try
        {
            foreach (var entry in manifest.Entries)
            {
                if (!_fileSystem.Path.IsPathFullyQualified(entry.TargetPath)
                    || !IsWithinRoot(manifest.WorkspaceRoot, entry.TargetPath)
                    || !targets.Add(entry.TargetPath)
                    || entry.DeleteMarkerPath is not null && !IsWithinRoot(manifest.WorkspaceRoot, entry.DeleteMarkerPath)
                    || entry.BackupPath is not null && !_fileSystem.Path.IsPathFullyQualified(GetArtifactPath(manifest.CommitId, entry.BackupPath))
                    || entry.StagedPath is not null && !_fileSystem.Path.IsPathFullyQualified(GetArtifactPath(manifest.CommitId, entry.StagedPath)))
                {
                    return false;
                }
            }

            return manifest.CreatedDirectories.All(path =>
                _fileSystem.Path.IsPathFullyQualified(path)
                && IsWithinRoot(manifest.WorkspaceRoot, path));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return false;
        }
    }

    private WorkspaceCommitManifest CreateInvalidManifest(string directory, string? loadedPath, string? workspaceRoot)
    {
        return new WorkspaceCommitManifest
        {
            CommitId = _fileSystem.Path.GetFileName(directory),
            LoadedPath = loadedPath ?? string.Empty,
            WorkspaceRoot = workspaceRoot ?? string.Empty,
            State = RecoveryState.RecoveryConflict,
            Entries = [],
            CreatedDirectories = [],
            Message = "The recovery manifest is malformed or contains unsafe paths.",
        };
    }

    private bool IsWithinRoot(string root, string path)
    {
        var relative = _fileSystem.Path.GetRelativePath(root, path);
        return relative != ".."
            && !relative.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !_fileSystem.Path.IsPathRooted(relative);
    }

    private void ValidateCommitId(string commitId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitId);
        if (commitId.IndexOfAny(_fileSystem.Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The commit identifier is not a valid path segment.", nameof(commitId));
        }
    }

    private async ValueTask WriteArtifactsAsync(WorkspaceCommitPlan plan, CancellationToken cancellationToken)
    {
        foreach (var artifact in plan.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetArtifactPath(plan.Manifest.CommitId, artifact.Key);
            var artifactDirectory = _fileSystem.Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException($"The recovery artifact '{path}' does not have a parent directory.");

            _fileSystem.Directory.CreateDirectory(artifactDirectory);
            await _atomicFileWriter.WriteAllBytesAsync(path, artifact.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        return _atomicFileWriter.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(value, _serializerOptions),
            _encoding,
            cancellationToken);
    }

    private ValueTask WriteOwnerAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken)
    {
        var owner = new WorkspaceCommitOwner
        {
            CommitId = manifest.CommitId,
            LoadedPath = manifest.LoadedPath,
            WorkspaceRoot = manifest.WorkspaceRoot,
        };

        return WriteJsonAsync(GetOwnerPath(manifest.CommitId), owner, cancellationToken);
    }
}
