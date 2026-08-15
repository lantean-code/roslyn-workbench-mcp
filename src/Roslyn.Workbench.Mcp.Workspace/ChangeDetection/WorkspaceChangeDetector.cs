namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceChangeDetector : IWorkspaceChangeDetector
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceProjectInputResolver _projectInputResolver;
    private readonly IWorkspaceInputChangeMonitorFactory _changeMonitorFactory;
    private readonly IWorkspacePathComparison _pathComparison;

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

    public WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties = null)
    {
        using var certification = BeginCertification(workspaceRoot);
        return BuildManifest(
            solution,
            loadedPath,
            workspaceRoot,
            certification,
            msBuildProperties);
    }

    public IWorkspaceInputCertification BeginCertification(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var changeMonitor = _changeMonitorFactory.Create(workspaceRoot);
        try
        {
            return new WorkspaceInputCertification(
                changeMonitor,
                _pathComparison);
        }
        catch
        {
            changeMonitor.Dispose();
            throw;
        }
    }

    public WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        IWorkspaceInputCertification certification,
        WorkspaceMsBuildProperties? msBuildProperties = null)
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
            msBuildProperties);
        return certification.Complete(manifest);
    }

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
        WorkspaceMsBuildProperties? msBuildProperties)
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

        foreach (var project in solution.Projects)
        {
            AddFile(files, directories, project.FilePath, pathPolicy);
            AddProjectDirectories(directories, project.FilePath, pathPolicy);
            AddDocuments(files, directories, project.Documents, pathPolicy);
            AddDocuments(files, directories, project.AdditionalDocuments, pathPolicy);
            AddDocuments(files, directories, project.AnalyzerConfigDocuments, pathPolicy);

            foreach (var analyzerReference in project.AnalyzerReferences)
            {
                AddFile(files, directories, analyzerReference.Display, pathPolicy);
            }

            foreach (var metadataReference in project.MetadataReferences.OfType<PortableExecutableReference>())
            {
                AddFile(files, directories, metadataReference.FilePath, pathPolicy);
            }

            var inputResolution = projectResolutions[project.Id];
            if (inputResolution.IsSucceeded)
            {
                foreach (var importPath in inputResolution.ImportedPaths)
                {
                    AddFile(files, directories, importPath, pathPolicy);
                }
            }
        }

        return new WorkspaceInputManifest
        {
            Directories = directories.Values.ToArray(),
            EvaluationFailures = evaluationFailures,
            Files = files.Values.ToArray(),
            PathPolicy = pathPolicy,
        };
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
        string? projectPath,
        WorkspaceInputPathPolicy pathPolicy)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        var projectFile = _fileSystem.FileInfo.New(projectPath);
        if (!projectFile.Exists)
        {
            return;
        }

        var projectDirectory = _fileSystem.Path.GetDirectoryName(projectFile.FullName);
        var directory = AddDirectory(directories, projectDirectory, pathPolicy);
        if (directory is null)
        {
            return;
        }

        foreach (var directoryPath in _fileSystem.Directory.EnumerateDirectories(directory.Path, "*", SearchOption.AllDirectories))
        {
            AddDirectory(directories, directoryPath, pathPolicy);
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
        WorkspaceInputPathPolicy pathPolicy)
    {
        foreach (var document in documents)
        {
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
