using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed class CommitRecoveryStore : ICommitRecoveryStore
{
    private const long _maximumOwnerBytes = 1024 * 1024;
    private const long _maximumLegacyStatusBytes = 1024 * 1024;
    private const long _maximumManifestBytes = 16 * 1024 * 1024;
    private const long _maximumArtifactBytes = 128 * 1024 * 1024;
    private const string _manifestFileName = "manifest.json";
    private const string _ownerFileName = "owner.json";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Encoding _encoding = new UTF8Encoding(false);

    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly IWorkspaceStateDirectorySecurity _stateDirectorySecurity;
    private readonly string _recoveryDirectory;

    public CommitRecoveryStore(
        IFileSystem fileSystem,
        IAtomicFileWriter atomicFileWriter,
        IWorkspacePathComparison pathComparison,
        IPhysicalPathContainment pathContainment,
        IWorkspaceStateDirectory stateDirectory,
        IWorkspaceStateDirectorySecurity stateDirectorySecurity)
    {
        _fileSystem = fileSystem;
        _atomicFileWriter = atomicFileWriter;
        _pathComparison = pathComparison;
        _pathContainment = pathContainment;
        _stateDirectorySecurity = stateDirectorySecurity;
        _recoveryDirectory = stateDirectory.RecoveryDirectory;
    }

    public async ValueTask PersistPlanAsync(WorkspaceCommitPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var commitDirectory = GetCommitDirectory(plan.Manifest.CommitId);
        var owner = CreateOwner(plan.Manifest);
        var ownerJson = SerializeJson(
            owner,
            _maximumOwnerBytes,
            "recovery owner record");

        var manifestJson = SerializeJson(
            plan.Manifest,
            _maximumManifestBytes,
            "recovery manifest");

        ValidateArtifactSizes(plan);

        _stateDirectorySecurity.EnsureDirectory(commitDirectory);
        await WriteJsonAsync(GetOwnerPath(plan.Manifest.CommitId), ownerJson, cancellationToken);
        await WriteArtifactsAsync(plan, cancellationToken);
        await WriteJsonAsync(GetManifestPath(plan.Manifest.CommitId), manifestJson, cancellationToken);
    }

    public ValueTask WriteManifestAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var commitDirectory = GetCommitDirectory(manifest.CommitId);
        var json = SerializeJson(
            manifest,
            _maximumManifestBytes,
            "recovery manifest");

        _stateDirectorySecurity.EnsureDirectory(commitDirectory);
        return WriteJsonAsync(GetManifestPath(manifest.CommitId), json, cancellationToken);
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
            if (!_pathContainment.TryGetStrictlyContainedPath(
                _recoveryDirectory,
                directory,
                out var containedDirectory))
            {
                manifests.Add(CreateInvalidManifest(directory, loadedPath: null, workspaceRoot: null));
                continue;
            }

            var path = GetManifestPathInDirectory(containedDirectory);
            if (!_fileSystem.File.Exists(path))
            {
                continue;
            }

            try
            {
                _stateDirectorySecurity.ValidateDirectory(containedDirectory);
                _stateDirectorySecurity.ValidateFile(path);
                ValidateFileSize(
                    path,
                    _maximumManifestBytes,
                    "recovery manifest");

                var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
                var manifest = JsonSerializer.Deserialize<WorkspaceCommitManifest>(json, _serializerOptions);
                if (manifest is not null && IsValidManifest(manifest, containedDirectory))
                {
                    manifests.Add(manifest);
                }
                else
                {
                    manifests.Add(CreateInvalidManifest(
                        containedDirectory,
                        manifest?.LoadedPath,
                        manifest?.WorkspaceRoot));
                }
            }
            catch (IOException)
            {
                manifests.Add(CreateInvalidManifest(
                    containedDirectory,
                    loadedPath: null,
                    workspaceRoot: null));
            }
            catch (InvalidDataException)
            {
                manifests.Add(CreateInvalidManifest(
                    containedDirectory,
                    loadedPath: null,
                    workspaceRoot: null));
            }
            catch (UnauthorizedAccessException)
            {
                manifests.Add(CreateInvalidManifest(
                    containedDirectory,
                    loadedPath: null,
                    workspaceRoot: null));
            }
            catch (JsonException)
            {
                manifests.Add(CreateInvalidManifest(
                    containedDirectory,
                    loadedPath: null,
                    workspaceRoot: null));
            }
        }

        return manifests;
    }

    public async ValueTask<IReadOnlyList<WorkspaceCommitOwner>> GetOrphanedCommitOwnersAsync(CancellationToken cancellationToken)
    {
        var evidence = await ReadOrphanedCommitEvidenceAsync(cancellationToken);
        return evidence.Owners;
    }

    public async ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        var manifests = await GetManifestsAsync(cancellationToken);
        var statuses = manifests
            .Select(manifest => new RecoveryStatus
            {
                CommitId = manifest.CommitId,
                SolutionPath = manifest.LoadedPath,
                WorkspaceRoot = manifest.WorkspaceRoot,
                State = manifest.State,
                Message = manifest.Message,
            }).ToList();

        var orphanedEvidence = await ReadOrphanedCommitEvidenceAsync(cancellationToken);
        statuses.AddRange(orphanedEvidence.Owners.Select(CreateOrphanedOwnerStatus));
        statuses.AddRange(orphanedEvidence.Conflicts);

        if (!_fileSystem.Directory.Exists(_recoveryDirectory))
        {
            return statuses;
        }

        foreach (var path in _fileSystem.Directory.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_pathContainment.TryGetStrictlyContainedPath(
                _recoveryDirectory,
                path,
                out var containedPath))
            {
                statuses.Add(await ReadLegacyStatusAsync(containedPath, cancellationToken));
            }
            else
            {
                statuses.Add(CreateLegacyStatus(
                    _fileSystem.Path.GetFileNameWithoutExtension(path),
                    legacy: null));
            }
        }

        return statuses;
    }

    public ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetLegacyStatusPath(status.CommitId);
        var json = SerializeJson(
            status,
            _maximumLegacyStatusBytes,
            "legacy recovery status");

        return WriteJsonAsync(path, json, cancellationToken);
    }

    public async ValueTask<byte[]> ReadArtifactAsync(string commitId, string relativePath, CancellationToken cancellationToken)
    {
        var path = GetArtifactPath(commitId, relativePath);
        _stateDirectorySecurity.ValidateFile(path);
        ValidateFileSize(
            path,
            _maximumArtifactBytes,
            "recovery artifact");

        return await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken);
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

    private async ValueTask<(IReadOnlyList<WorkspaceCommitOwner> Owners, IReadOnlyList<RecoveryStatus> Conflicts)>
        ReadOrphanedCommitEvidenceAsync(CancellationToken cancellationToken)
    {
        var owners = new List<WorkspaceCommitOwner>();
        var conflicts = new List<RecoveryStatus>();
        if (!_fileSystem.Directory.Exists(_recoveryDirectory))
        {
            return (owners, conflicts);
        }

        foreach (var directory in _fileSystem.Directory.EnumerateDirectories(_recoveryDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_pathContainment.TryGetStrictlyContainedPath(
                _recoveryDirectory,
                directory,
                out var containedDirectory))
            {
                conflicts.Add(CreateInvalidOwnerStatus(directory, owner: null));
                continue;
            }

            if (_fileSystem.File.Exists(GetManifestPathInDirectory(containedDirectory)))
            {
                continue;
            }

            var ownerPath = GetOwnerPathInDirectory(containedDirectory);
            if (!_fileSystem.File.Exists(ownerPath))
            {
                continue;
            }

            try
            {
                _stateDirectorySecurity.ValidateDirectory(containedDirectory);
                _stateDirectorySecurity.ValidateFile(ownerPath);
                ValidateFileSize(
                    ownerPath,
                    _maximumOwnerBytes,
                    "recovery owner record");

                var json = await _fileSystem.File.ReadAllTextAsync(ownerPath, cancellationToken);
                var owner = JsonSerializer.Deserialize<WorkspaceCommitOwner>(json, _serializerOptions);
                if (owner is not null
                    && owner.Version == 2
                    && string.Equals(
                        owner.CommitId,
                        _fileSystem.Path.GetFileName(containedDirectory),
                        _pathComparison.Comparison)
                    && _fileSystem.Path.IsPathFullyQualified(owner.LoadedPath)
                    && _fileSystem.Path.IsPathFullyQualified(owner.WorkspaceRoot))
                {
                    owners.Add(owner);
                }
                else
                {
                    conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner));
                }
            }
            catch (IOException)
            {
                conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner: null));
            }
            catch (InvalidDataException)
            {
                conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner: null));
            }
            catch (UnauthorizedAccessException)
            {
                conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner: null));
            }
            catch (JsonException)
            {
                conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner: null));
            }
        }

        return (owners, conflicts);
    }

    private async ValueTask<RecoveryStatus> ReadLegacyStatusAsync(string path, CancellationToken cancellationToken)
    {
        var commitId = _fileSystem.Path.GetFileNameWithoutExtension(path);
        try
        {
            _stateDirectorySecurity.ValidateFile(path);
            ValidateFileSize(
                path,
                _maximumLegacyStatusBytes,
                "legacy recovery status");

            var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
            var legacy = JsonSerializer.Deserialize<RecoveryStatus>(json, _serializerOptions);
            return CreateLegacyStatus(commitId, legacy);
        }
        catch (IOException)
        {
            return CreateLegacyStatus(commitId, legacy: null);
        }
        catch (InvalidDataException)
        {
            return CreateLegacyStatus(commitId, legacy: null);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateLegacyStatus(commitId, legacy: null);
        }
        catch (JsonException)
        {
            return CreateLegacyStatus(commitId, legacy: null);
        }
    }

    private RecoveryStatus CreateInvalidOwnerStatus(string directory, WorkspaceCommitOwner? owner)
    {
        return new RecoveryStatus
        {
            CommitId = _fileSystem.Path.GetFileName(directory),
            SolutionPath = GetSafeAbsolutePath(owner?.LoadedPath),
            WorkspaceRoot = GetSafeAbsolutePath(owner?.WorkspaceRoot),
            State = RecoveryState.RecoveryConflict,
            Message = "The recovery owner record is malformed or unreadable.",
        };
    }

    private static RecoveryStatus CreateOrphanedOwnerStatus(WorkspaceCommitOwner owner)
    {
        return new RecoveryStatus
        {
            CommitId = owner.CommitId,
            SolutionPath = owner.LoadedPath,
            WorkspaceRoot = owner.WorkspaceRoot,
            State = RecoveryState.RecoveryConflict,
            Message = "The commit was interrupted before its durable manifest was prepared.",
        };
    }

    private static RecoveryStatus CreateLegacyStatus(string commitId, RecoveryStatus? legacy)
    {
        return new RecoveryStatus
        {
            CommitId = commitId,
            SolutionPath = legacy?.SolutionPath ?? string.Empty,
            WorkspaceRoot = legacy?.WorkspaceRoot ?? string.Empty,
            State = RecoveryState.RecoveryConflict,
            Message = "Legacy recovery evidence cannot be restored automatically.",
        };
    }

    private string GetSafeAbsolutePath(string? path)
    {
        return path is not null && _fileSystem.Path.IsPathFullyQualified(path) ? path : string.Empty;
    }

    private string GetCommitDirectory(string commitId)
    {
        ValidateCommitId(commitId);
        var candidate = _fileSystem.Path.Combine(_recoveryDirectory, commitId);
        return GetRequiredStrictlyContainedPath(_recoveryDirectory, candidate);
    }

    private string GetLegacyStatusPath(string commitId)
    {
        ValidateCommitId(commitId);
        var candidate = _fileSystem.Path.Combine(_recoveryDirectory, $"{commitId}.json");
        return GetRequiredStrictlyContainedPath(_recoveryDirectory, candidate);
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
        ValidateCommitId(commitId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (!TryGetArtifactPath(commitId, relativePath, out var path))
        {
            throw new InvalidDataException("The recovery artifact path escapes its commit directory.");
        }

        return path;
    }

    private bool IsValidManifest(WorkspaceCommitManifest manifest, string directory)
    {
        if (manifest.Version != 2
            || string.IsNullOrWhiteSpace(manifest.CommitId)
            || string.IsNullOrWhiteSpace(manifest.LoadedPath)
            || string.IsNullOrWhiteSpace(manifest.WorkspaceRoot)
            || manifest.Entries is null
            || manifest.CreatedDirectories is null
            || !HasValidCommitIdCharacters(manifest.CommitId)
            || !string.Equals(manifest.CommitId, _fileSystem.Path.GetFileName(directory), _pathComparison.Comparison)
            || !_fileSystem.Path.IsPathFullyQualified(manifest.LoadedPath)
            || !_fileSystem.Path.IsPathFullyQualified(manifest.WorkspaceRoot)
            || !_pathContainment.TryGetContainedPath(
                manifest.WorkspaceRoot,
                manifest.LoadedPath,
                out _))
        {
            return false;
        }

        var targets = new HashSet<string>(_pathComparison.GetComparer(manifest.WorkspaceRoot));
        foreach (var entry in manifest.Entries)
        {
            if (entry is null || !IsValidEntry(manifest, entry, targets))
            {
                return false;
            }
        }

        var createdDirectories = new HashSet<string>(_pathComparison.GetComparer(manifest.WorkspaceRoot));
        foreach (var path in manifest.CreatedDirectories)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !_fileSystem.Path.IsPathFullyQualified(path)
                || !_pathContainment.TryGetStrictlyContainedPath(
                    manifest.WorkspaceRoot,
                    path,
                    out _)
                || !createdDirectories.Add(path))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsValidEntry(
        WorkspaceCommitManifest manifest,
        WorkspaceCommitEntry entry,
        HashSet<string> targets)
    {
        if (string.IsNullOrWhiteSpace(entry.TargetPath)
            || !_fileSystem.Path.IsPathFullyQualified(entry.TargetPath)
            || !_pathContainment.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                entry.TargetPath,
                out _)
            || !targets.Add(entry.TargetPath))
        {
            return false;
        }

        return entry.Operation switch
        {
            WorkspaceFileOperation.Create => !entry.OriginalExists
                && entry.OriginalHash is null
                && IsValidHash(entry.IntendedHash)
                && entry.BackupPath is null
                && HasRequiredArtifactPath(manifest.CommitId, entry.StagedPath)
                && entry.DeleteMarkerPath is null,
            WorkspaceFileOperation.Replace => entry.OriginalExists
                && IsValidHash(entry.OriginalHash)
                && IsValidHash(entry.IntendedHash)
                && HasRequiredArtifactPath(manifest.CommitId, entry.BackupPath)
                && HasRequiredArtifactPath(manifest.CommitId, entry.StagedPath)
                && entry.DeleteMarkerPath is null,
            WorkspaceFileOperation.Delete => entry.OriginalExists
                && IsValidHash(entry.OriginalHash)
                && entry.IntendedHash is null
                && HasRequiredArtifactPath(manifest.CommitId, entry.BackupPath)
                && entry.StagedPath is null
                && IsValidDeleteMarker(manifest, entry),
            _ => false,
        };
    }

    private bool IsValidDeleteMarker(WorkspaceCommitManifest manifest, WorkspaceCommitEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DeleteMarkerPath)
            || !_fileSystem.Path.IsPathFullyQualified(entry.DeleteMarkerPath)
            || !_pathContainment.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                entry.DeleteMarkerPath,
                out _))
        {
            return false;
        }

        var expectedPath = $"{entry.TargetPath}.{manifest.CommitId}.delete";
        return string.Equals(
            expectedPath,
            entry.DeleteMarkerPath,
            _pathComparison.GetComparison(manifest.WorkspaceRoot));
    }

    private bool HasRequiredArtifactPath(string commitId, string? relativePath)
    {
        return TryGetArtifactPath(commitId, relativePath, out var path)
            && _fileSystem.Path.IsPathFullyQualified(path);
    }

    private static bool IsValidHash(string? hash)
    {
        if (hash is not { Length: 64 })
        {
            return false;
        }

        foreach (var character in hash)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetArtifactPath(
        string commitId,
        string? relativePath,
        [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            var commitDirectory = _fileSystem.Path.Combine(_recoveryDirectory, commitId);
            if (!_pathContainment.TryGetStrictlyContainedPath(
                _recoveryDirectory,
                commitDirectory,
                out var root))
            {
                return false;
            }

            var candidate = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(root, relativePath));
            if (!_pathContainment.TryGetStrictlyContainedPath(root, candidate, out path))
            {
                return false;
            }

            return true;
        }
        catch (ArgumentException)
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

    private void ValidateCommitId(string commitId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitId);
        if (!HasValidCommitIdCharacters(commitId))
        {
            throw new ArgumentException("The commit identifier is not a valid path segment.", nameof(commitId));
        }
    }

    private bool HasValidCommitIdCharacters(string commitId)
    {
        return commitId.IndexOfAny(_fileSystem.Path.GetInvalidFileNameChars()) < 0;
    }

    private string GetRequiredStrictlyContainedPath(string rootDirectory, string candidatePath)
    {
        if (!_pathContainment.TryGetStrictlyContainedPath(
            rootDirectory,
            candidatePath,
            out var containedPath))
        {
            throw new InvalidDataException(
                $"The recovery path '{candidatePath}' resolves outside its recovery directory.");
        }

        return containedPath;
    }

    private async ValueTask WriteArtifactsAsync(WorkspaceCommitPlan plan, CancellationToken cancellationToken)
    {
        foreach (var artifact in plan.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetArtifactPath(plan.Manifest.CommitId, artifact.Key);
            var artifactDirectory = _fileSystem.Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException($"The recovery artifact '{path}' does not have a parent directory.");

            _stateDirectorySecurity.EnsureDirectory(artifactDirectory);
            await _atomicFileWriter.WriteAllBytesAsync(
                path,
                artifact.Value,
                AtomicFileAccess.OwnerOnly,
                cancellationToken);
        }
    }

    private ValueTask WriteJsonAsync(string path, string json, CancellationToken cancellationToken)
    {
        return _atomicFileWriter.WriteAllTextAsync(
            path,
            json,
            _encoding,
            AtomicFileAccess.OwnerOnly,
            cancellationToken);
    }

    private void ValidateFileSize(string path, long maximumBytes, string description)
    {
        var length = _fileSystem.FileInfo.New(path).Length;
        if (length > maximumBytes)
        {
            throw new InvalidDataException(
                $"The {description} '{path}' exceeds the supported maximum size.");
        }
    }

    private static void ValidateArtifactSizes(WorkspaceCommitPlan plan)
    {
        foreach (var artifact in plan.Artifacts)
        {
            if (artifact.Value.Length > _maximumArtifactBytes)
            {
                throw new InvalidDataException(
                    $"The recovery artifact '{artifact.Key}' exceeds the supported maximum size.");
            }
        }
    }

    private static string SerializeJson<T>(T value, long maximumBytes, string description)
    {
        var json = JsonSerializer.Serialize(value, _serializerOptions);
        if (_encoding.GetByteCount(json) > maximumBytes)
        {
            throw new InvalidDataException(
                $"The {description} exceeds the supported maximum size.");
        }

        return json;
    }

    private static WorkspaceCommitOwner CreateOwner(WorkspaceCommitManifest manifest)
    {
        return new WorkspaceCommitOwner
        {
            CommitId = manifest.CommitId,
            LoadedPath = manifest.LoadedPath,
            WorkspaceRoot = manifest.WorkspaceRoot,
        };
    }
}
