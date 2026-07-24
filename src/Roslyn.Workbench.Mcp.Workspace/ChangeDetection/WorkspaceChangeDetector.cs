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

    public WorkspaceInputManifest BuildManifest(Solution solution, string loadedPath, string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loadedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        using var phase = WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace-open",
            WorkbenchPerformanceEventSource.ManifestConstructionPhase);

        var changeMonitor = _changeMonitorFactory.Create(workspaceRoot);

        try
        {
            return BuildManifest(solution, loadedPath, workspaceRoot, changeMonitor);
        }
        catch
        {
            changeMonitor.Dispose();
            throw;
        }
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

    private WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        IWorkspaceInputChangeMonitor changeMonitor)
    {
        var pathComparer = _pathComparison.GetComparer(workspaceRoot);
        var files = new Dictionary<string, WorkspaceInputFileFingerprint>(pathComparer);
        var directories = new Dictionary<string, WorkspaceInputDirectoryFingerprint>(pathComparer);
        var evaluationFailures = new List<WorkspaceProjectInputFailure>();
        var projectResolutions = new Dictionary<ProjectId, WorkspaceProjectInputResolution>();
        var artifactRoots = new List<string>
        {
            _fileSystem.Path.Combine(workspaceRoot, ".vs"),
        };

        var protectedPaths = new List<string> { loadedPath };
        var resolutionCache = new Dictionary<string, WorkspaceProjectInputResolution>(pathComparer);

        foreach (var project in solution.Projects)
        {
            var inputResolution = ResolveProjectInputs(project.FilePath, resolutionCache);
            projectResolutions.Add(project.Id, inputResolution);
            if (!inputResolution.IsSucceeded)
            {
                evaluationFailures.Add(inputResolution.Failure);
            }
            else
            {
                AddProjectArtifactRoots(project, inputResolution, artifactRoots);
            }

            if (!string.IsNullOrWhiteSpace(project.FilePath))
            {
                protectedPaths.Add(project.FilePath);
            }
        }

        var pathPolicy = WorkspaceInputPathPolicy.Create(
            artifactRoots,
            protectedPaths,
            _pathComparison.GetComparison(workspaceRoot));

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

        var manifest = new WorkspaceInputManifest
        {
            ChangeMonitor = changeMonitor,
            Directories = directories.Values.ToArray(),
            EvaluationFailures = evaluationFailures,
            Files = files.Values.ToArray(),
            PathPolicy = pathPolicy,
        };

        changeMonitor.Track(manifest);

        return manifest;
    }

    private void AddFile(
        Dictionary<string, WorkspaceInputFileFingerprint> files,
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
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

        files[file.FullName] = new WorkspaceInputFileFingerprint
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
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
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
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
        string? path,
        WorkspaceInputPathPolicy pathPolicy)
    {
        if (string.IsNullOrWhiteSpace(path) || !pathPolicy.ShouldTrack(path))
        {
            return null;
        }

        var directory = _fileSystem.DirectoryInfo.New(path);
        if (directory.Exists)
        {
            directories[directory.FullName] = new WorkspaceInputDirectoryFingerprint
            {
                Path = directory.FullName,
            };
        }

        return !directory.Exists
            ? null
            : directories[directory.FullName];
    }

    private void AddDocuments(
        Dictionary<string, WorkspaceInputFileFingerprint> files,
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
        IEnumerable<TextDocument> documents,
        WorkspaceInputPathPolicy pathPolicy)
    {
        foreach (var document in documents)
        {
            if (pathPolicy.ShouldTrack(document.FilePath))
            {
                AddFile(files, directories, document.FilePath, pathPolicy);
            }
        }
    }

    private void AddProjectArtifactRoots(
        Project project,
        WorkspaceProjectInputResolution inputResolution,
        List<string> artifactRoots)
    {
        if (inputResolution.ArtifactRoots.Count == 0)
        {
            AddProjectFallbackArtifactRoots(project.FilePath, artifactRoots);
        }
        else
        {
            foreach (var artifactRoot in inputResolution.ArtifactRoots)
            {
                artifactRoots.Add(artifactRoot);
            }
        }

        AddParentPath(project.OutputFilePath, artifactRoots);
        AddPath(project.CompilationOutputInfo.GeneratedFilesOutputDirectory, artifactRoots);
    }

    private void AddProjectFallbackArtifactRoots(
        string? projectPath,
        List<string> artifactRoots)
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

        artifactRoots.Add(_fileSystem.Path.Combine(projectDirectory, "bin"));
        artifactRoots.Add(_fileSystem.Path.Combine(projectDirectory, "obj"));
    }

    private void AddParentPath(
        string? path,
        List<string> artifactRoots)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddPath(_fileSystem.Path.GetDirectoryName(path), artifactRoots);
    }

    private WorkspaceProjectInputResolution ResolveProjectInputs(
        string? projectPath,
        Dictionary<string, WorkspaceProjectInputResolution> resolutionCache)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return _projectInputResolver.Resolve(projectPath);
        }

        if (resolutionCache.TryGetValue(projectPath, out var cachedResolution))
        {
            return cachedResolution;
        }

        var resolution = _projectInputResolver.Resolve(projectPath);
        resolutionCache.Add(projectPath, resolution);
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
