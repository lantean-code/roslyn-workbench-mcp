namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceChangeDetector : IWorkspaceChangeDetector
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceProjectInputResolver _projectInputResolver;

    public WorkspaceChangeDetector(IFileSystem fileSystem, IWorkspaceProjectInputResolver projectInputResolver)
    {
        _fileSystem = fileSystem;
        _projectInputResolver = projectInputResolver;
    }

    public WorkspaceInputManifest BuildManifest(Solution solution, string loadedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loadedPath);
        using var phase = WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace-open",
            WorkbenchPerformanceEventSource.ManifestConstructionPhase);

        var files = new Dictionary<string, WorkspaceInputFileFingerprint>(StringComparer.Ordinal);
        var directories = new Dictionary<string, WorkspaceInputDirectoryFingerprint>(StringComparer.Ordinal);
        var evaluationFailures = new List<WorkspaceProjectInputFailure>();

        AddFile(files, directories, loadedPath);

        foreach (var project in solution.Projects)
        {
            AddFile(files, directories, project.FilePath);
            AddProjectDirectories(directories, project.FilePath);

            foreach (var document in project.Documents.Concat(project.AdditionalDocuments).Concat(project.AnalyzerConfigDocuments))
            {
                AddFile(files, directories, document.FilePath);
            }

            foreach (var analyzerReference in project.AnalyzerReferences)
            {
                AddFile(files, directories, analyzerReference.Display);
            }

            foreach (var metadataReference in project.MetadataReferences.OfType<PortableExecutableReference>())
            {
                AddFile(files, directories, metadataReference.FilePath);
            }

            var inputResolution = _projectInputResolver.Resolve(project.FilePath);
            if (!inputResolution.IsSucceeded)
            {
                evaluationFailures.Add(inputResolution.Failure);
                continue;
            }

            foreach (var importPath in inputResolution.Paths)
            {
                AddFile(files, directories, importPath);
            }
        }

        return new WorkspaceInputManifest
        {
            Directories = directories.Values.ToArray(),
            EvaluationFailures = evaluationFailures,
            Files = files.Values.ToArray(),
        };
    }

    public bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken)
    {
        using var phase = WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace",
            WorkbenchPerformanceEventSource.ExternalChangeDetectionPhase);

        if (manifest is null)
        {
            return false;
        }

        if (!manifest.IsComplete)
        {
            return true;
        }

        foreach (var directory in manifest.Directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = _fileSystem.DirectoryInfo.New(directory.Path);
            if (!current.Exists || current.LastWriteTimeUtc != directory.LastWriteTimeUtc)
            {
                return true;
            }
        }

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = _fileSystem.FileInfo.New(file.Path);
            if (!current.Exists || current.LastWriteTimeUtc != file.LastWriteTimeUtc || current.Length != file.Length)
            {
                return true;
            }
        }

        return false;
    }

    private void AddFile(
        Dictionary<string, WorkspaceInputFileFingerprint> files,
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
        string? path)
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
        AddDirectory(directories, _fileSystem.Path.GetDirectoryName(file.FullName));
    }

    private void AddProjectDirectories(
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
        string? projectPath)
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
        var directory = AddDirectory(directories, projectDirectory);
        if (directory is null)
        {
            return;
        }

        foreach (var directoryPath in _fileSystem.Directory.EnumerateDirectories(directory.Path, "*", SearchOption.AllDirectories))
        {
            AddDirectory(directories, directoryPath);
        }
    }

    private WorkspaceInputDirectoryFingerprint? AddDirectory(
        IDictionary<string, WorkspaceInputDirectoryFingerprint> directories,
        string? path)
    {
        if (path is null || !ShouldTrackDirectory(path))
        {
            return null;
        }

        var directory = _fileSystem.DirectoryInfo.New(path);
        if (directory.Exists)
        {
            directories[directory.FullName] = new WorkspaceInputDirectoryFingerprint
            {
                Path = directory.FullName,
                LastWriteTimeUtc = directory.LastWriteTimeUtc,
            };
        }

        return !directory.Exists
            ? null
            : directories[directory.FullName];
    }

    private bool ShouldTrackDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var canonical = _fileSystem.Path.TrimEndingDirectorySeparator(path);
        var segments = canonical.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return !segments.Any(static segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, ".vs", StringComparison.OrdinalIgnoreCase));
    }
}
