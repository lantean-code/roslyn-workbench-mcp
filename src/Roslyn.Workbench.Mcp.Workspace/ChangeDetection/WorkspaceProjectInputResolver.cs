namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Evaluates project imports, source-item globs and output roots under design-time MSBuild properties.
/// </summary>
internal sealed class WorkspaceProjectInputResolver : IWorkspaceProjectInputResolver
{
    private static readonly string[] _workspaceItemTypes = ["Compile", "AdditionalFiles", "EditorConfigFiles"];

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

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceProjectInputResolver"/> class.
    /// </summary>
    /// <param name="pathComparison">The path normalizer and comparer used to remove duplicate inputs.</param>
    public WorkspaceProjectInputResolver(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    /// <inheritdoc/>
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
            var globalProperties = msBuildProperties?.ToGlobalProperties();
            var effectiveGlobalProperties = WorkspaceDesignTimeGlobalProperties.Create(globalProperties);
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection(effectiveGlobalProperties);
            var project = new Microsoft.Build.Evaluation.Project(
                projectPath,
                effectiveGlobalProperties,
                toolsVersion: null,
                projectCollection,
                Microsoft.Build.Evaluation.ProjectLoadSettings.RecordEvaluatedItemElements);

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
            var itemGlobs = ResolveItemGlobs(project);
            return WorkspaceProjectInputResolution.Succeeded(
                importedPaths.ToArray(),
                artifactRoots,
                itemGlobs);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceProjectInputResolution.Failed(projectPath, exception.Message);
        }
    }

    private WorkspaceEvaluatedItemGlob[] ResolveItemGlobs(Microsoft.Build.Evaluation.Project project)
    {
        var itemGlobs = new List<WorkspaceEvaluatedItemGlob>();
        foreach (var itemType in _workspaceItemTypes)
        {
            foreach (var globResult in project.GetAllGlobs(itemType))
            {
                var searchRoots = ResolveSearchRoots(project.DirectoryPath, globResult.IncludeGlobs);
                if (searchRoots.Count == 0)
                {
                    continue;
                }

                var matcher = new MsBuildWorkspaceItemGlobMatcher(globResult.MsBuildGlob);
                var itemGlob = new WorkspaceEvaluatedItemGlob(matcher, searchRoots);

                itemGlobs.Add(itemGlob);
            }
        }

        return itemGlobs.ToArray();
    }

    private List<string> ResolveSearchRoots(
        string projectDirectory,
        IEnumerable<string> includeGlobs)
    {
        var searchRoots = new List<string>();
        var uniqueSearchRoots = new HashSet<FileSystemPathKey>();
        foreach (var includeGlob in includeGlobs)
        {
            var parsedGlob = Microsoft.Build.Globbing.MSBuildGlob.Parse(
                projectDirectory,
                includeGlob);

            if (!parsedGlob.IsLegal)
            {
                continue;
            }

            var searchRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(parsedGlob.FixedDirectoryPart));

            if (uniqueSearchRoots.Add(_pathComparison.CreateKey(searchRoot)))
            {
                searchRoots.Add(searchRoot);
            }
        }

        return searchRoots;
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
