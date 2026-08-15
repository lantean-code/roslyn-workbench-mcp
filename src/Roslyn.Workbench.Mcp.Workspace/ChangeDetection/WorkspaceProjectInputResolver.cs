namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceProjectInputResolver : IWorkspaceProjectInputResolver
{
    private static readonly string[] _artifactPathPropertyNames =
    [
        "ArtifactsPath",
        "BaseIntermediateOutputPath",
        "IntermediateOutputPath",
        "MSBuildProjectExtensionsPath",
        "BaseOutputPath",
        "OutputPath",
        "OutDir",
        "PublishDir",
        "PackageOutputPath",
    ];

    private readonly IWorkspacePathComparison _pathComparison;

    public WorkspaceProjectInputResolver(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    public WorkspaceProjectInputResolution Resolve(
        string? projectPath,
        WorkspaceMsBuildProperties? msBuildProperties = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return WorkspaceProjectInputResolution.Succeeded();
        }

        if (!File.Exists(projectPath))
        {
            return WorkspaceProjectInputResolution.Failed(
                projectPath,
                "The project file does not exist.");
        }

        try
        {
            var globalProperties = msBuildProperties?.ToGlobalProperties()
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var effectiveGlobalProperties = new Dictionary<string, string>(globalProperties, StringComparer.OrdinalIgnoreCase);
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection(effectiveGlobalProperties);
            var project = projectCollection.LoadProject(projectPath);
            var importedPaths = new List<string>();
            var uniqueImportedPaths = new HashSet<FileSystemPathKey>();
            foreach (var import in project.Imports)
            {
                var path = import.ImportedProject?.FullPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                var fullPathKey = _pathComparison.CreateKey(fullPath);
                if (uniqueImportedPaths.Add(fullPathKey))
                {
                    importedPaths.Add(fullPath);
                }
            }

            var artifactRoots = ResolveArtifactRoots(project);
            return WorkspaceProjectInputResolution.Succeeded(
                importedPaths.ToArray(),
                artifactRoots);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceProjectInputResolution.Failed(projectPath, exception.Message);
        }
    }

    private string[] ResolveArtifactRoots(Microsoft.Build.Evaluation.Project project)
    {
        var artifactRoots = new List<string>();
        var uniqueArtifactRoots = new HashSet<FileSystemPathKey>();
        foreach (var propertyName in _artifactPathPropertyNames)
        {
            var propertyValue = project.GetPropertyValue(propertyName);
            if (!TryResolvePath(project.DirectoryPath, propertyValue, out var artifactRoot)
                || !uniqueArtifactRoots.Add(_pathComparison.CreateKey(artifactRoot)))
            {
                continue;
            }

            artifactRoots.Add(artifactRoot);
        }

        if (artifactRoots.Count == 0)
        {
            AddFallbackArtifactRoot(project.DirectoryPath, "bin", artifactRoots, uniqueArtifactRoots);
            AddFallbackArtifactRoot(project.DirectoryPath, "obj", artifactRoots, uniqueArtifactRoots);
        }

        return artifactRoots.ToArray();
    }

    private void AddFallbackArtifactRoot(
        string projectDirectory,
        string directoryName,
        List<string> artifactRoots,
        HashSet<FileSystemPathKey> uniqueArtifactRoots)
    {
        var artifactRoot = Path.GetFullPath(Path.Combine(projectDirectory, directoryName));
        if (uniqueArtifactRoots.Add(_pathComparison.CreateKey(artifactRoot)))
        {
            artifactRoots.Add(artifactRoot);
        }
    }

    private static bool TryResolvePath(
        string projectDirectory,
        string? path,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? resolvedPath)
    {
        resolvedPath = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var platformPath = path
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            resolvedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(platformPath, projectDirectory));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
