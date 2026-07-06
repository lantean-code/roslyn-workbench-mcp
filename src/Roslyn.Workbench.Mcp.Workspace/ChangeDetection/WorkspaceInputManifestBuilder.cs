namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal static class WorkspaceInputManifestBuilder
{
    public static WorkspaceInputManifest Build(Solution solution, string loadedPath)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentException.ThrowIfNullOrWhiteSpace(loadedPath);

        var filePaths = new HashSet<string>(StringComparer.Ordinal);
        var directoryPaths = new HashSet<string>(StringComparer.Ordinal);

        AddFilePath(filePaths, directoryPaths, loadedPath);

        foreach (var project in solution.Projects)
        {
            AddFilePath(filePaths, directoryPaths, project.FilePath);
            AddProjectDirectories(directoryPaths, project.FilePath);

            foreach (var document in project.Documents)
            {
                AddFilePath(filePaths, directoryPaths, document.FilePath);
            }

            foreach (var document in project.AdditionalDocuments)
            {
                AddFilePath(filePaths, directoryPaths, document.FilePath);
            }

            foreach (var document in project.AnalyzerConfigDocuments)
            {
                AddFilePath(filePaths, directoryPaths, document.FilePath);
            }

            foreach (var analyzerReference in project.AnalyzerReferences)
            {
                AddFilePath(filePaths, directoryPaths, analyzerReference.Display);
            }

            foreach (var metadataReference in project.MetadataReferences.OfType<PortableExecutableReference>())
            {
                AddFilePath(filePaths, directoryPaths, metadataReference.FilePath);
            }

            foreach (var importPath in MsBuildProjectUtilities.GetEvaluatedInputPaths(project.FilePath))
            {
                AddFilePath(filePaths, directoryPaths, importPath);
            }
        }

        return new WorkspaceInputManifest
        {
            Directories = directoryPaths
                .Select(WorkspaceInputDirectoryFingerprint.Create)
                .ToArray(),
            Files = filePaths
                .Select(WorkspaceInputFileFingerprint.Create)
                .ToArray(),
        };
    }

    private static void AddFilePath(ISet<string> filePaths, ISet<string> directoryPaths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(path);
        if (File.Exists(normalizedPath))
        {
            filePaths.Add(normalizedPath);
            AddDirectoryPath(directoryPaths, Path.GetDirectoryName(normalizedPath));
        }
    }

    private static void AddProjectDirectories(ISet<string> directoryPaths, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return;
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return;
        }

        AddDirectoryPath(directoryPaths, projectDirectory);

        foreach (var directoryPath in Directory.EnumerateDirectories(projectDirectory, "*", SearchOption.AllDirectories))
        {
            if (ShouldTrackDirectory(directoryPath))
            {
                AddDirectoryPath(directoryPaths, directoryPath);
            }
        }
    }

    private static void AddDirectoryPath(ISet<string> directoryPaths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(path);
        if (Directory.Exists(normalizedPath) && ShouldTrackDirectory(normalizedPath))
        {
            directoryPaths.Add(normalizedPath);
        }
    }

    private static bool ShouldTrackDirectory(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return !string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase);
    }
}
