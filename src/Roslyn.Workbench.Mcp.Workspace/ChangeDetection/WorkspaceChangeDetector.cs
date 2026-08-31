namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Builds and validates Workspace input manifests from Roslyn documents, MSBuild evaluation and filesystem metadata.
/// </summary>
internal sealed class WorkspaceChangeDetector : IWorkspaceChangeDetector
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceProjectInputResolver _projectInputResolver;
    private readonly IWorkspaceInputChangeMonitorFactory _changeMonitorFactory;
    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceChangeDetector"/> class.
    /// </summary>
    /// <param name="fileSystem">The filesystem abstraction used to fingerprint inputs.</param>
    /// <param name="projectInputResolver">The resolver for project imports, item globs and artifact roots.</param>
    /// <param name="changeMonitorFactory">The factory for load-time root watchers.</param>
    /// <param name="pathComparison">The platform-aware path identity service.</param>
    public WorkspaceChangeDetector(
        IFileSystem fileSystem,
        IWorkspaceProjectInputResolver projectInputResolver,
        IWorkspaceInputChangeMonitorFactory changeMonitorFactory,
        IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _projectInputResolver = projectInputResolver;
        _changeMonitorFactory = changeMonitorFactory;
        _pathComparison = pathComparison;
    }

    /// <inheritdoc/>
    public IWorkspaceInputCertification BeginCertification(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var changeMonitor = _changeMonitorFactory.Create(workspaceRoot);
        try
        {
            var certification = new WorkspaceInputCertification(
                changeMonitor,
                _pathComparison);

            return certification;
        }
        catch
        {
            changeMonitor.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        IWorkspaceInputCertification certification,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loadedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        using var phase = WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace-open",
            WorkbenchPerformanceEventSource.ManifestConstructionPhase);

        using var manifest = CreateManifest(
            solution,
            loadedPath,
            workspaceRoot,
            msBuildProperties,
            cancellationToken);

        return certification.Complete(manifest);
    }

    /// <inheritdoc/>
    public bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken)
    {
        using var phase = WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace",
            WorkbenchPerformanceEventSource.ExternalChangeDetectionPhase);

        if (manifest.Change is not null)
        {
            return true;
        }

        manifest.ChangeMonitor?.WaitForPendingEvents(cancellationToken);
        var monitoredChange = manifest.ChangeMonitor?.Change;
        if (monitoredChange is not null)
        {
            manifest.RecordChange(monitoredChange);
            return true;
        }

        if (!manifest.IsComplete)
        {
            RecordChange(
                manifest,
                WorkspaceInputChangeDetectionSource.ManifestValidation,
                WorkspaceInputChangeKind.ManifestIncomplete);

            return true;
        }

        foreach (var directory in manifest.Directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (manifest.IgnoredPaths.Contains(_pathComparison.CreateKey(directory.Path)))
            {
                continue;
            }

            var current = _fileSystem.DirectoryInfo.New(directory.Path);
            if (!current.Exists)
            {
                RecordChange(
                    manifest,
                    WorkspaceInputChangeDetectionSource.MetadataPolling,
                    WorkspaceInputChangeKind.Deleted,
                    directory.Path);

                return true;
            }
        }

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (manifest.IgnoredPaths.Contains(_pathComparison.CreateKey(file.Path)))
            {
                continue;
            }

            var current = _fileSystem.FileInfo.New(file.Path);
            if (!current.Exists)
            {
                RecordChange(
                    manifest,
                    WorkspaceInputChangeDetectionSource.MetadataPolling,
                    WorkspaceInputChangeKind.Deleted,
                    file.Path);

                return true;
            }

            if (current.LastWriteTimeUtc != file.LastWriteTimeUtc || current.Length != file.Length)
            {
                RecordChange(
                    manifest,
                    WorkspaceInputChangeDetectionSource.MetadataPolling,
                    WorkspaceInputChangeKind.MetadataChanged,
                    file.Path);

                return true;
            }
        }

        return false;
    }

    private WorkspaceInputManifest CreateManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<FileSystemPathKey, WorkspaceInputFileFingerprint>();
        var directories = new Dictionary<FileSystemPathKey, WorkspaceInputDirectoryFingerprint>();
        var evaluationFailures = new List<WorkspaceProjectInputFailure>();
        var projectResolutions = new Dictionary<ProjectId, WorkspaceProjectInputResolution>();
        var excludedDirectoryRoots = new List<string>
        {
            _fileSystem.Path.Combine(workspaceRoot, ".vs"),
        };

        var protectedPaths = new List<string> { loadedPath };
        var resolutionCache = new Dictionary<FileSystemPathKey, WorkspaceProjectInputResolution>();

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputResolution = ResolveProjectInputs(
                project.FilePath,
                msBuildProperties,
                resolutionCache);

            projectResolutions.Add(project.Id, inputResolution);
            if (!inputResolution.IsSucceeded)
            {
                evaluationFailures.Add(inputResolution.Failure);
            }
            else
            {
                AddProjectMonitoringExclusions(project, inputResolution, excludedDirectoryRoots);
            }

            if (!string.IsNullOrWhiteSpace(project.FilePath))
            {
                protectedPaths.Add(project.FilePath);
            }
        }

        var pathPolicy = WorkspaceInputPathPolicy.Create(
            excludedDirectoryRoots,
            protectedPaths,
            _pathComparison);

        AddFile(files, directories, loadedPath, pathPolicy);
        AddProjectDirectories(
            directories,
            solution.Projects,
            pathPolicy,
            cancellationToken);

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddFile(files, directories, project.FilePath, pathPolicy);
            AddDocuments(files, directories, project.Documents, pathPolicy, cancellationToken);
            AddDocuments(files, directories, project.AdditionalDocuments, pathPolicy, cancellationToken);
            AddDocuments(files, directories, project.AnalyzerConfigDocuments, pathPolicy, cancellationToken);

            foreach (var analyzerReference in project.AnalyzerReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddFile(files, directories, analyzerReference.Display, pathPolicy);
            }

            foreach (var metadataReference in project.MetadataReferences.OfType<PortableExecutableReference>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddFile(files, directories, metadataReference.FilePath, pathPolicy);
            }

            var inputResolution = projectResolutions[project.Id];
            if (inputResolution.IsSucceeded)
            {
                foreach (var importPath in inputResolution.ImportedPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddFile(files, directories, importPath, pathPolicy);
                }
            }
        }

        var externalInputMemberships = CreateExternalInputMemberships(
            solution,
            projectResolutions,
            workspaceRoot,
            cancellationToken);

        var manifest = new WorkspaceInputManifest
        {
            Directories = directories.Values.ToArray(),
            EvaluationFailures = evaluationFailures,
            ExternalInputMemberships = externalInputMemberships,
            Files = files.Values.ToArray(),
            PathPolicy = pathPolicy,
        };

        return manifest;
    }

    private List<WorkspaceExternalInputMembership> CreateExternalInputMemberships(
        Solution solution,
        IReadOnlyDictionary<ProjectId, WorkspaceProjectInputResolution> projectResolutions,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var externalGlobRoots = new List<(WorkspaceEvaluatedItemGlob Glob, FileSystemPathKey Root)>();
        var uniqueRoots = new HashSet<FileSystemPathKey>();
        foreach (var resolution in projectResolutions.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!resolution.IsSucceeded)
            {
                continue;
            }

            foreach (var itemGlob in resolution.ItemGlobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var searchRoot in itemGlob.SearchRoots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var normalizedSearchRoot = _fileSystem.Path.TrimEndingDirectorySeparator(
                        _fileSystem.Path.GetFullPath(searchRoot));

                    if (ContainsPath(workspaceRoot, normalizedSearchRoot))
                    {
                        continue;
                    }

                    var searchRootKey = _pathComparison.CreateKey(normalizedSearchRoot);
                    externalGlobRoots.Add((itemGlob, searchRootKey));
                    uniqueRoots.Add(searchRootKey);
                }
            }
        }

        if (externalGlobRoots.Count == 0)
        {
            return [];
        }

        var minimalRoots = RemoveNestedRoots(uniqueRoots);
        var loadedDocumentPaths = GetLoadedDocumentPaths(solution, cancellationToken);
        var memberships = new List<WorkspaceExternalInputMembership>(minimalRoots.Count);
        foreach (var minimalRoot in minimalRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var globs = GetGlobsForRoot(
                minimalRoot,
                externalGlobRoots,
                cancellationToken);

            var loadedPaths = GetLoadedPathsForGlobs(
                minimalRoot,
                globs,
                loadedDocumentPaths,
                cancellationToken);

            var membership = new WorkspaceExternalInputMembership(
                minimalRoot,
                globs,
                loadedPaths);

            memberships.Add(membership);
        }

        return memberships;
    }

    private List<WorkspaceEvaluatedItemGlob> GetGlobsForRoot(
        FileSystemPathKey minimalRoot,
        IEnumerable<(WorkspaceEvaluatedItemGlob Glob, FileSystemPathKey Root)> externalGlobRoots,
        CancellationToken cancellationToken)
    {
        var globs = new List<WorkspaceEvaluatedItemGlob>();
        var uniqueGlobs = new HashSet<WorkspaceEvaluatedItemGlob>();
        foreach (var (glob, root) in externalGlobRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ContainsPath(minimalRoot.Path, root.Path) && uniqueGlobs.Add(glob))
            {
                globs.Add(glob);
            }
        }

        return globs;
    }

    private HashSet<FileSystemPathKey> GetLoadedPathsForGlobs(
        FileSystemPathKey root,
        IReadOnlyList<WorkspaceEvaluatedItemGlob> globs,
        IEnumerable<FileSystemPathKey> loadedDocumentPaths,
        CancellationToken cancellationToken)
    {
        var loadedPaths = new HashSet<FileSystemPathKey>();
        foreach (var path in loadedDocumentPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ContainsPath(root.Path, path.Path))
            {
                continue;
            }

            foreach (var glob in globs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!glob.Matches(path.Path))
                {
                    continue;
                }

                loadedPaths.Add(path);
                break;
            }
        }

        return loadedPaths;
    }

    private FileSystemPathKey[] GetLoadedDocumentPaths(
        Solution solution,
        CancellationToken cancellationToken)
    {
        var paths = new HashSet<FileSystemPathKey>();
        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddDocumentPaths(paths, project.Documents, cancellationToken);
            AddDocumentPaths(paths, project.AdditionalDocuments, cancellationToken);
            AddDocumentPaths(paths, project.AnalyzerConfigDocuments, cancellationToken);
        }

        return paths.ToArray();
    }

    private void AddDocumentPaths(
        HashSet<FileSystemPathKey> paths,
        IEnumerable<TextDocument> documents,
        CancellationToken cancellationToken)
    {
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(document.FilePath))
            {
                paths.Add(_pathComparison.CreateKey(document.FilePath));
            }
        }
    }

    private List<FileSystemPathKey> RemoveNestedRoots(IEnumerable<FileSystemPathKey> roots)
    {
        var orderedRoots = roots.ToList();
        orderedRoots.Sort(static (left, right) => left.Path.Length.CompareTo(right.Path.Length));

        var minimalRoots = new List<FileSystemPathKey>(orderedRoots.Count);
        foreach (var root in orderedRoots)
        {
            var isNested = false;
            foreach (var existingRoot in minimalRoots)
            {
                if (ContainsPath(existingRoot.Path, root.Path))
                {
                    isNested = true;
                    break;
                }
            }

            if (isNested)
            {
                continue;
            }

            minimalRoots.Add(root);
        }

        return minimalRoots;
    }

    private bool ContainsPath(string root, string path)
    {
        var comparison = _pathComparison.GetComparison(root);
        if (string.Equals(root, path, comparison))
        {
            return true;
        }

        var rootPrefix = _fileSystem.Path.EndsInDirectorySeparator(root)
            ? root
            : root + _fileSystem.Path.DirectorySeparatorChar;

        return path.StartsWith(rootPrefix, comparison);
    }

    private void AddFile(
        Dictionary<FileSystemPathKey, WorkspaceInputFileFingerprint> files,
        IDictionary<FileSystemPathKey, WorkspaceInputDirectoryFingerprint> directories,
        string? path,
        WorkspaceInputPathPolicy pathPolicy)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var file = _fileSystem.FileInfo.New(path);
        if (!file.Exists)
        {
            return;
        }

        var filePathKey = _pathComparison.CreateKey(file.FullName);
        files[filePathKey] = new WorkspaceInputFileFingerprint
        {
            Path = file.FullName,
            LastWriteTimeUtc = file.LastWriteTimeUtc,
            Length = file.Length,
        };

        AddDirectory(
            directories,
            _fileSystem.Path.GetDirectoryName(file.FullName),
            pathPolicy);
    }

    private void AddProjectDirectories(
        IDictionary<FileSystemPathKey, WorkspaceInputDirectoryFingerprint> directories,
        IEnumerable<Project> projects,
        WorkspaceInputPathPolicy pathPolicy,
        CancellationToken cancellationToken)
    {
        var projectDirectoryRoots = GetProjectDirectoryRoots(projects, cancellationToken);
        var minimalRoots = RemoveNestedRoots(projectDirectoryRoots);
        var visitedDirectories = new HashSet<FileSystemPathKey>();
        foreach (var root in minimalRoots)
        {
            AddProjectDirectoryTree(
                directories,
                root.Path,
                pathPolicy,
                visitedDirectories,
                cancellationToken);
        }
    }

    private HashSet<FileSystemPathKey> GetProjectDirectoryRoots(
        IEnumerable<Project> projects,
        CancellationToken cancellationToken)
    {
        var roots = new HashSet<FileSystemPathKey>();
        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(project.FilePath))
            {
                continue;
            }

            var projectFile = _fileSystem.FileInfo.New(project.FilePath);
            if (!projectFile.Exists)
            {
                continue;
            }

            var projectDirectory = _fileSystem.Path.GetDirectoryName(projectFile.FullName);
            if (!string.IsNullOrWhiteSpace(projectDirectory))
            {
                roots.Add(_pathComparison.CreateKey(projectDirectory));
            }
        }

        return roots;
    }

    private void AddProjectDirectoryTree(
        IDictionary<FileSystemPathKey, WorkspaceInputDirectoryFingerprint> directories,
        string root,
        WorkspaceInputPathPolicy pathPolicy,
        HashSet<FileSystemPathKey> visitedDirectories,
        CancellationToken cancellationToken)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);
        while (pendingDirectories.TryPop(out var path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pathKey = _pathComparison.CreateKey(path);
            if (!visitedDirectories.Add(pathKey))
            {
                continue;
            }

            var directory = AddDirectory(directories, path, pathPolicy);
            if (directory is null)
            {
                continue;
            }

            var childPaths = _fileSystem.Directory.EnumerateDirectories(
                directory.Path,
                "*",
                SearchOption.TopDirectoryOnly);

            foreach (var childPath in childPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pathPolicy.ShouldMonitor(childPath))
                {
                    pendingDirectories.Push(childPath);
                }
            }
        }
    }

    private WorkspaceInputDirectoryFingerprint? AddDirectory(
        IDictionary<FileSystemPathKey, WorkspaceInputDirectoryFingerprint> directories,
        string? path,
        WorkspaceInputPathPolicy pathPolicy)
    {
        if (string.IsNullOrWhiteSpace(path) || !pathPolicy.ShouldMonitor(path))
        {
            return null;
        }

        var directory = _fileSystem.DirectoryInfo.New(path);
        if (!directory.Exists)
        {
            return null;
        }

        var fingerprint = new WorkspaceInputDirectoryFingerprint
        {
            Path = directory.FullName,
        };

        var directoryPathKey = _pathComparison.CreateKey(directory.FullName);
        directories[directoryPathKey] = fingerprint;
        return fingerprint;
    }

    private void AddDocuments(
        Dictionary<FileSystemPathKey, WorkspaceInputFileFingerprint> files,
        IDictionary<FileSystemPathKey, WorkspaceInputDirectoryFingerprint> directories,
        IEnumerable<TextDocument> documents,
        WorkspaceInputPathPolicy pathPolicy,
        CancellationToken cancellationToken)
    {
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddFile(files, directories, document.FilePath, pathPolicy);
        }
    }

    private void AddProjectMonitoringExclusions(
        Project project,
        WorkspaceProjectInputResolution inputResolution,
        List<string> excludedDirectoryRoots)
    {
        if (inputResolution.ArtifactRoots.Count == 0)
        {
            AddProjectFallbackMonitoringExclusions(project.FilePath, excludedDirectoryRoots);
        }
        else
        {
            foreach (var artifactRoot in inputResolution.ArtifactRoots)
            {
                excludedDirectoryRoots.Add(artifactRoot);
            }
        }

        AddParentPath(project.OutputFilePath, excludedDirectoryRoots);
        AddPath(project.CompilationOutputInfo.GeneratedFilesOutputDirectory, excludedDirectoryRoots);
    }

    private void AddProjectFallbackMonitoringExclusions(
        string? projectPath,
        List<string> excludedDirectoryRoots)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        var projectDirectory = _fileSystem.Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return;
        }

        excludedDirectoryRoots.Add(_fileSystem.Path.Combine(projectDirectory, "bin"));
        excludedDirectoryRoots.Add(_fileSystem.Path.Combine(projectDirectory, "obj"));
    }

    private void AddParentPath(
        string? path,
        List<string> excludedDirectoryRoots)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddPath(_fileSystem.Path.GetDirectoryName(path), excludedDirectoryRoots);
    }

    private WorkspaceProjectInputResolution ResolveProjectInputs(
        string? projectPath,
        WorkspaceMsBuildProperties? msBuildProperties,
        Dictionary<FileSystemPathKey, WorkspaceProjectInputResolution> resolutionCache)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return _projectInputResolver.Resolve(projectPath, msBuildProperties);
        }

        var projectPathKey = _pathComparison.CreateKey(projectPath);
        if (resolutionCache.TryGetValue(projectPathKey, out var cachedResolution))
        {
            return cachedResolution;
        }

        var resolution = _projectInputResolver.Resolve(projectPath, msBuildProperties);
        resolutionCache.Add(projectPathKey, resolution);
        return resolution;
    }

    private static void AddPath(
        string? path,
        List<string> paths)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            paths.Add(path);
        }
    }

    private static void RecordChange(
        WorkspaceInputManifest manifest,
        WorkspaceInputChangeDetectionSource detectionSource,
        WorkspaceInputChangeKind kind,
        string? path = null)
    {
        var change = new WorkspaceInputChange
        {
            DetectionSource = detectionSource,
            Kind = kind,
            Path = path,
        };

        manifest.RecordChange(change);
    }
}
