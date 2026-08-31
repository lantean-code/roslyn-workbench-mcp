using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Stores durable workspace commit recovery plans, artifacts, manifests, and status evidence.
/// </summary>
internal sealed class CommitRecoveryStore : ICommitRecoveryStore
{
    private const string _manifestFileName = "manifest.json";
    private const string _ownerFileName = "owner.json";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Encoding _encoding = new UTF8Encoding(false);

    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IWorkspacePathNormalizer _pathNormalizer;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly IWorkspaceStateDirectorySecurity _stateDirectorySecurity;
    private readonly CommitRecoveryLimits _limits;
    private readonly string _recoveryDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitRecoveryStore"/> class.
    /// </summary>
    /// <param name="fileSystem">The file-system abstraction used for storage operations.</param>
    /// <param name="atomicFileWriter">The writer that persists recovery state atomically.</param>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    /// <param name="pathNormalizer">The service used to normalize workspace paths.</param>
    /// <param name="pathContainment">The service used to test whether paths belong to the workspace.</param>
    /// <param name="stateDirectory">The directory used for workspace ownership and recovery state.</param>
    /// <param name="stateDirectorySecurity">The component that applies access controls to the state directory.</param>
    /// <param name="limits">The size limits enforced for persisted recovery state.</param>
    public CommitRecoveryStore(
        IFileSystem fileSystem,
        IAtomicFileWriter atomicFileWriter,
        IWorkspacePathComparison pathComparison,
        IWorkspacePathNormalizer pathNormalizer,
        IPhysicalPathContainment pathContainment,
        IWorkspaceStateDirectory stateDirectory,
        IWorkspaceStateDirectorySecurity stateDirectorySecurity,
        CommitRecoveryLimits limits)
    {
        _fileSystem = fileSystem;
        _atomicFileWriter = atomicFileWriter;
        _pathComparison = pathComparison;
        _pathNormalizer = pathNormalizer;
        _pathContainment = pathContainment;
        _stateDirectorySecurity = stateDirectorySecurity;
        _limits = limits;
        _recoveryDirectory = stateDirectory.RecoveryDirectory;
    }

    /// <inheritdoc/>
    public async ValueTask<CommitRecoveryPlanPersistenceResult> PersistPlanAsync(
        WorkspaceCommitPlan plan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var commitDirectory = GetCommitDirectory(plan.Manifest.CommitId);
        var owner = CreateOwner(plan.Manifest);
        var ownerJson = JsonSerializer.Serialize(owner, _serializerOptions);
        var manifestJson = JsonSerializer.Serialize(plan.Manifest, _serializerOptions);
        var committedManifest = plan.Manifest with { State = RecoveryState.Committed };
        var committedManifestJson = JsonSerializer.Serialize(committedManifest, _serializerOptions);
        var capacity = ValidatePlanCapacity(
            plan,
            ownerJson,
            manifestJson,
            committedManifestJson);

        if (!capacity.IsPersisted)
        {
            return capacity;
        }

        _stateDirectorySecurity.EnsureDirectory(commitDirectory);
        await WriteJsonAsync(GetOwnerPath(plan.Manifest.CommitId), ownerJson, cancellationToken);
        await WriteArtifactsAsync(plan, cancellationToken);
        await WriteJsonAsync(GetManifestPath(plan.Manifest.CommitId), manifestJson, cancellationToken);
        return CommitRecoveryPlanPersistenceResult.Persisted();
    }

    /// <inheritdoc/>
    public ValueTask WriteManifestAsync(WorkspaceCommitManifest manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var commitDirectory = GetCommitDirectory(manifest.CommitId);
        var json = SerializeJson(
            manifest,
            _limits.MaximumManifestBytes,
            "recovery manifest");

        _stateDirectorySecurity.EnsureDirectory(commitDirectory);
        return WriteJsonAsync(GetManifestPath(manifest.CommitId), json, cancellationToken);
    }

    /// <inheritdoc/>
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
                    _limits.MaximumManifestBytes,
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

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceCommitOwner>> GetOrphanedCommitOwnersAsync(CancellationToken cancellationToken)
    {
        var evidence = await ReadOrphanedCommitEvidenceAsync(cancellationToken);
        return evidence.Owners;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        var manifests = await GetManifestsAsync(cancellationToken);
        var statuses = manifests
            .Select(manifest => new RecoveryStatus
            {
                CommitId = manifest.CommitId,
                SolutionPath = manifest.LoadedPath,
                WorkspaceRoot = manifest.WorkspaceRoot,
                HasMalformedWorkspaceIdentity = manifest.HasMalformedWorkspaceIdentity,
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

    /// <inheritdoc/>
    public ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetLegacyStatusPath(status.CommitId);
        var json = SerializeJson(
            status,
            _limits.MaximumLegacyStatusBytes,
            "legacy recovery status");

        return WriteJsonAsync(path, json, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]> ReadArtifactAsync(string commitId, string relativePath, CancellationToken cancellationToken)
    {
        var path = GetArtifactPath(commitId, relativePath);
        _stateDirectorySecurity.ValidateFile(path);
        ValidateFileSize(
            path,
            _limits.MaximumArtifactBytes,
            "recovery artifact");

        return await _fileSystem.File.ReadAllBytesAsync(path, cancellationToken);
    }

    /// <inheritdoc/>
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
                    _limits.MaximumOwnerBytes,
                    "recovery owner record");

                var json = await _fileSystem.File.ReadAllTextAsync(ownerPath, cancellationToken);
                var owner = JsonSerializer.Deserialize<WorkspaceCommitOwner>(json, _serializerOptions);
                if (owner is null)
                {
                    conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner));
                    continue;
                }

                var hasValidMetadata = owner.Version == 1
                    && string.Equals(
                        owner.CommitId,
                        _fileSystem.Path.GetFileName(containedDirectory),
                        _pathComparison.GetComparison(containedDirectory));

                if (!hasValidMetadata)
                {
                    conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner));
                    continue;
                }

                var hasValidLoadedPath = TryGetSafeNormalizedPath(owner.LoadedPath, allowMissing: false, out var loadedPath);
                var hasValidWorkspaceRoot = TryGetSafeNormalizedPath(owner.WorkspaceRoot, allowMissing: false, out var workspaceRoot);

                if (!hasValidLoadedPath || !hasValidWorkspaceRoot)
                {
                    conflicts.Add(CreateInvalidOwnerStatus(containedDirectory, owner));
                    continue;
                }

                var normalizedOwner = owner with
                {
                    LoadedPath = loadedPath,
                    WorkspaceRoot = workspaceRoot,
                };

                owners.Add(normalizedOwner);
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
                _limits.MaximumLegacyStatusBytes,
                "legacy recovery status");

            var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
            var legacy = JsonSerializer.Deserialize<LegacyRecoveryStatus>(json, _serializerOptions);
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
        var hasValidLoadedPath = TryGetSafeNormalizedPath(owner?.LoadedPath, allowMissing: false, out var loadedPath);
        var hasValidWorkspaceRoot = TryGetSafeNormalizedPath(owner?.WorkspaceRoot, allowMissing: false, out var workspaceRoot);

        return new RecoveryStatus
        {
            CommitId = _fileSystem.Path.GetFileName(directory),
            SolutionPath = loadedPath,
            WorkspaceRoot = workspaceRoot,
            HasMalformedWorkspaceIdentity = !hasValidLoadedPath || !hasValidWorkspaceRoot,
            State = RecoveryState.RecoveryConflict,
            Message = "The recovery owner record is malformed or unreadable.",
        };
    }

    private RecoveryStatus CreateOrphanedOwnerStatus(WorkspaceCommitOwner owner)
    {
        var hasValidLoadedPath = TryGetSafeNormalizedPath(owner.LoadedPath, allowMissing: false, out var loadedPath);
        var hasValidWorkspaceRoot = TryGetSafeNormalizedPath(owner.WorkspaceRoot, allowMissing: false, out var workspaceRoot);

        return new RecoveryStatus
        {
            CommitId = owner.CommitId,
            SolutionPath = loadedPath,
            WorkspaceRoot = workspaceRoot,
            HasMalformedWorkspaceIdentity = !hasValidLoadedPath || !hasValidWorkspaceRoot,
            State = RecoveryState.RecoveryConflict,
            Message = "The commit was interrupted before its durable manifest was prepared.",
        };
    }

    private RecoveryStatus CreateLegacyStatus(string commitId, LegacyRecoveryStatus? legacy)
    {
        var hasValidSolutionPath = TryGetSafeNormalizedPath(legacy?.SolutionPath, allowMissing: false, out var solutionPath);
        var hasValidWorkspaceRoot = TryGetSafeNormalizedPath(legacy?.WorkspaceRoot, allowMissing: true, out var workspaceRoot);

        return new RecoveryStatus
        {
            CommitId = commitId,
            SolutionPath = solutionPath,
            WorkspaceRoot = workspaceRoot,
            HasMalformedWorkspaceIdentity = !hasValidSolutionPath || !hasValidWorkspaceRoot,
            State = RecoveryState.RecoveryConflict,
            Message = "Legacy recovery evidence cannot be restored automatically.",
        };
    }

    private bool TryGetSafeNormalizedPath(string? path, bool allowMissing, out string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            normalizedPath = string.Empty;
            return allowMissing;
        }

        if (!_fileSystem.Path.IsPathFullyQualified(path)
            || !_pathNormalizer.TryGetFullPath(path, out normalizedPath))
        {
            normalizedPath = string.Empty;
            return false;
        }

        return true;
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
        if (manifest.Version != 1
            || string.IsNullOrWhiteSpace(manifest.CommitId)
            || string.IsNullOrWhiteSpace(manifest.LoadedPath)
            || string.IsNullOrWhiteSpace(manifest.WorkspaceRoot)
            || manifest.Entries is null
            || manifest.CreatedDirectories is null
            || !HasValidCommitIdCharacters(manifest.CommitId)
            || !string.Equals(manifest.CommitId, _fileSystem.Path.GetFileName(directory), _pathComparison.GetComparison(directory))
            || !_fileSystem.Path.IsPathFullyQualified(manifest.LoadedPath)
            || !_fileSystem.Path.IsPathFullyQualified(manifest.WorkspaceRoot)
            || !_pathContainment.TryGetContainedPath(
                manifest.WorkspaceRoot,
                manifest.LoadedPath,
                out _))
        {
            return false;
        }

        var targets = new HashSet<FileSystemPathKey>();
        foreach (var entry in manifest.Entries)
        {
            if (entry is null || !IsValidEntry(manifest, entry, targets))
            {
                return false;
            }
        }

        var createdDirectories = new HashSet<FileSystemPathKey>();
        foreach (var path in manifest.CreatedDirectories)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !_fileSystem.Path.IsPathFullyQualified(path)
                || !_pathContainment.TryGetStrictlyContainedPath(
                    manifest.WorkspaceRoot,
                    path,
                    out _))
            {
                return false;
            }

            var pathKey = _pathComparison.CreateKey(path);
            if (!createdDirectories.Add(pathKey))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsValidEntry(
        WorkspaceCommitManifest manifest,
        WorkspaceCommitEntry entry,
        HashSet<FileSystemPathKey> targets)
    {
        if (string.IsNullOrWhiteSpace(entry.TargetPath)
            || !_fileSystem.Path.IsPathFullyQualified(entry.TargetPath)
            || !_pathContainment.TryGetStrictlyContainedPath(
                manifest.WorkspaceRoot,
                entry.TargetPath,
                out _))
        {
            return false;
        }

        var targetPathKey = _pathComparison.CreateKey(entry.TargetPath);
        if (!targets.Add(targetPathKey))
        {
            return false;
        }

        return entry.Operation switch
        {
            WorkspaceFileOperation.Create => HasValidUnixFileMode(entry.OriginalUnixFileMode, requiresMode: false, permitsMode: false)
                && HasValidUnixFileMode(entry.IntendedUnixFileMode, requiresMode: false, permitsMode: !OperatingSystem.IsWindows())
                && !entry.OriginalExists
                && entry.OriginalHash is null
                && IsValidHash(entry.IntendedHash)
                && entry.BackupPath is null
                && HasRequiredArtifactPath(manifest.CommitId, entry.StagedPath)
                && entry.DeleteMarkerPath is null,
            WorkspaceFileOperation.Replace => HasValidUnixFileMode(
                    entry.OriginalUnixFileMode,
                    requiresMode: !OperatingSystem.IsWindows(),
                    permitsMode: !OperatingSystem.IsWindows())
                && HasValidUnixFileMode(
                    entry.IntendedUnixFileMode,
                    requiresMode: !OperatingSystem.IsWindows(),
                    permitsMode: !OperatingSystem.IsWindows())
                && entry.OriginalExists
                && IsValidHash(entry.OriginalHash)
                && IsValidHash(entry.IntendedHash)
                && HasRequiredArtifactPath(manifest.CommitId, entry.BackupPath)
                && HasRequiredArtifactPath(manifest.CommitId, entry.StagedPath)
                && entry.DeleteMarkerPath is null,
            WorkspaceFileOperation.Delete => HasValidUnixFileMode(
                    entry.OriginalUnixFileMode,
                    requiresMode: !OperatingSystem.IsWindows(),
                    permitsMode: !OperatingSystem.IsWindows())
                && HasValidUnixFileMode(entry.IntendedUnixFileMode, requiresMode: false, permitsMode: false)
                && entry.OriginalExists
                && IsValidHash(entry.OriginalHash)
                && entry.IntendedHash is null
                && HasRequiredArtifactPath(manifest.CommitId, entry.BackupPath)
                && entry.StagedPath is null
                && IsValidDeleteMarker(manifest, entry),
            _ => false,
        };
    }

    private static bool HasValidUnixFileMode(UnixFileMode? unixFileMode, bool requiresMode, bool permitsMode)
    {
        if (unixFileMode is not { } mode)
        {
            return !requiresMode;
        }

        if (!permitsMode)
        {
            return false;
        }

        const UnixFileMode validModes = UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherWrite
            | UnixFileMode.OtherExecute
            | UnixFileMode.SetUser
            | UnixFileMode.SetGroup
            | UnixFileMode.StickyBit;

        return (mode & ~validModes) == 0;
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
            _pathComparison.GetComparison(entry.TargetPath));
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
        var hasValidLoadedPath = TryGetSafeNormalizedPath(loadedPath, allowMissing: false, out var normalizedLoadedPath);
        var hasValidWorkspaceRoot = TryGetSafeNormalizedPath(workspaceRoot, allowMissing: false, out var normalizedWorkspaceRoot);

        return new WorkspaceCommitManifest
        {
            CommitId = _fileSystem.Path.GetFileName(directory),
            LoadedPath = normalizedLoadedPath,
            WorkspaceRoot = normalizedWorkspaceRoot,
            HasMalformedWorkspaceIdentity = !hasValidLoadedPath || !hasValidWorkspaceRoot,
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

    private CommitRecoveryPlanPersistenceResult ValidatePlanCapacity(
        WorkspaceCommitPlan plan,
        string ownerJson,
        string manifestJson,
        string committedManifestJson)
    {
        var ownerBytes = _encoding.GetByteCount(ownerJson);
        if (ownerBytes > _limits.MaximumOwnerBytes)
        {
            return CreateCapacityExceededResult(
                "recovery owner record",
                ownerBytes,
                _limits.MaximumOwnerBytes);
        }

        var manifestBytes = Math.Max(
            _encoding.GetByteCount(manifestJson),
            _encoding.GetByteCount(committedManifestJson));

        if (manifestBytes > _limits.MaximumManifestBytes)
        {
            return CreateCapacityExceededResult(
                "recovery manifest",
                manifestBytes,
                _limits.MaximumManifestBytes);
        }

        foreach (var artifact in plan.Artifacts)
        {
            if (artifact.Value.Length > _limits.MaximumArtifactBytes)
            {
                return CreateCapacityExceededResult(
                    $"recovery artifact '{artifact.Key}'",
                    artifact.Value.Length,
                    _limits.MaximumArtifactBytes);
            }
        }

        return CommitRecoveryPlanPersistenceResult.Persisted();
    }

    private static CommitRecoveryPlanPersistenceResult CreateCapacityExceededResult(
        string description,
        long actualBytes,
        long maximumBytes)
    {
        return CommitRecoveryPlanPersistenceResult.CapacityExceeded(
            $"The {description} requires {actualBytes} bytes, exceeding the supported maximum of {maximumBytes} bytes.");
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
