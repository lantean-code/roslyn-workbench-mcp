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

    public WorkspaceProjectInputResolution Resolve(string? projectPath)
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
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);
            var comparer = _pathComparison.GetComparer(projectPath);

            var importedPaths = new List<string>();
            var uniqueImportedPaths = new HashSet<string>(comparer);
            foreach (var import in project.Imports)
            {
                var path = import.ImportedProject?.FullPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                if (uniqueImportedPaths.Add(fullPath))
                {
                    importedPaths.Add(fullPath);
                }
            }

            var artifactRoots = ResolveArtifactRoots(project, comparer);
            return WorkspaceProjectInputResolution.Succeeded(
                importedPaths.ToArray(),
                artifactRoots);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceProjectInputResolution.Failed(projectPath, exception.Message);
        }
    }

    private static string[] ResolveArtifactRoots(
        Microsoft.Build.Evaluation.Project project,
        StringComparer comparer)
    {
        var artifactRoots = new List<string>();
        var uniqueArtifactRoots = new HashSet<string>(comparer);
        foreach (var propertyName in _artifactPathPropertyNames)
        {
            var propertyValue = project.GetPropertyValue(propertyName);
            if (!TryResolvePath(project.DirectoryPath, propertyValue, out var artifactRoot)
                || !uniqueArtifactRoots.Add(artifactRoot))
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

    private static void AddFallbackArtifactRoot(
        string projectDirectory,
        string directoryName,
        List<string> artifactRoots,
        HashSet<string> uniqueArtifactRoots)
    {
        var artifactRoot = Path.GetFullPath(Path.Combine(projectDirectory, directoryName));
        if (uniqueArtifactRoots.Add(artifactRoot))
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
