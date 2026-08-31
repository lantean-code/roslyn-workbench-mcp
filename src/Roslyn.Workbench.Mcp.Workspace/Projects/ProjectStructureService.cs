namespace Roslyn.Workbench.Mcp.Workspace.Projects;

/// <summary>
/// Resolves project target frameworks and constructs the loaded solution hierarchy.
/// </summary>
internal sealed class ProjectStructureService : IProjectStructureService
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IWorkspacePathNormalizer _pathNormalizer;
    private readonly IWorkspaceMsBuildPropertiesProvider _msBuildPropertiesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectStructureService"/> class.
    /// </summary>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    /// <param name="pathNormalizer">The service used to normalize workspace paths.</param>
    /// <param name="msBuildPropertiesProvider">The MSBuild properties provider.</param>
    public ProjectStructureService(
        IWorkspacePathComparison pathComparison,
        IWorkspacePathNormalizer pathNormalizer,
        IWorkspaceMsBuildPropertiesProvider msBuildPropertiesProvider)
    {
        _pathComparison = pathComparison;
        _pathNormalizer = pathNormalizer;
        _msBuildPropertiesProvider = msBuildPropertiesProvider;
    }

    /// <summary>
    /// Gets the target frameworks inferred for a loaded project.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="project">The loaded project to inspect.</param>
    /// <returns>The project's distinct target-framework identities.</returns>
    public ProjectTargetFrameworksResult GetTargetFrameworks(Guid workspaceId, Project project)
    {
        return GetTargetFrameworks(workspaceId, project.FilePath);
    }

    /// <summary>
    /// Gets the target frameworks evaluated from a project file.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="projectPath">The project file path being evaluated or resolved.</param>
    /// <returns>The project's distinct target-framework identities.</returns>
    public ProjectTargetFrameworksResult GetTargetFrameworks(Guid workspaceId, string? projectPath)
    {
        var globalProperties = GetGlobalProperties(workspaceId);
        using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection(globalProperties);
        try
        {
            return EvaluateProjectTargetFrameworks(projectPath, projectCollection);
        }
        finally
        {
            projectCollection.UnloadAllProjects();
        }
    }

    /// <summary>
    /// Gets the union of target frameworks used by selected projects.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="projects">The projects included in the selected workspace scope.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The distinct target-framework identities in stable order.</returns>
    public IReadOnlyList<ProjectTargetFrameworksResult> GetTargetFrameworks(
        Guid workspaceId,
        IReadOnlyList<Project> projects,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new ProjectTargetFrameworksResult[projects.Count];
        var resultsByPath = new Dictionary<FileSystemPathKey, ProjectTargetFrameworksResult>();
        var globalProperties = GetGlobalProperties(workspaceId);
        using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection(globalProperties);

        try
        {
            for (var index = 0; index < projects.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var project = projects[index];
                var projectPath = project.FilePath;
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    results[index] = ProjectTargetFrameworksResult.Succeeded();
                    continue;
                }

                var projectPathKey = _pathComparison.CreateKey(projectPath);
                if (!resultsByPath.TryGetValue(projectPathKey, out var result))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result = EvaluateProjectTargetFrameworks(projectPath, projectCollection);
                    cancellationToken.ThrowIfCancellationRequested();
                    resultsByPath.Add(projectPathKey, result);
                }

                results[index] = result;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return results;
        }
        finally
        {
            projectCollection.UnloadAllProjects();
        }
    }

    /// <summary>
    /// Builds the solution-folder and project hierarchy for a loaded workspace.
    /// </summary>
    /// <param name="workspace">The loaded workspace whose hierarchy should be projected.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the solution hierarchy.</returns>
    public async Task<SolutionHierarchyResult> GetSolutionHierarchyAsync(
        WorkspaceIdentity workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestedLoadedPath = workspace.LoadedPath;
        if (string.IsNullOrWhiteSpace(requestedLoadedPath))
        {
            return SolutionHierarchyResult.Succeeded();
        }

        if (!_pathNormalizer.TryGetFullPath(requestedLoadedPath, out var loadedPath)
            || !_pathNormalizer.TryGetFullPath(workspace.WorkspaceRoot, out var workspaceRoot))
        {
            return SolutionHierarchyResult.Failed(
                "Could not load solution hierarchy because the workspace paths are invalid.");
        }

        if (!File.Exists(loadedPath))
        {
            return SolutionHierarchyResult.Failed(
                $"Could not load solution hierarchy because workspace file '{loadedPath}' does not exist.");
        }

        var serializer = SolutionSerializers.GetSerializerByMoniker(loadedPath);
        if (serializer is null)
        {
            return SolutionHierarchyResult.Succeeded();
        }

        try
        {
            var model = await serializer.OpenAsync(loadedPath, cancellationToken);
            var folders = model.SolutionFolders
                .Select(static folder => CreateSolutionFolderInfo(folder))
                .OrderBy(static folder => folder.Path, StringComparer.Ordinal)
                .ToArray();

            var solutionDirectory = Path.GetDirectoryName(loadedPath.AsSpan()).ToString();

            var observedProjectPaths = new HashSet<FileSystemPathKey>();
            var projectFolderPaths = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var project in model.SolutionProjects)
            {
                if (!_pathNormalizer.TryGetFullPath(project.FilePath, solutionDirectory, out var fullProjectPath)
                    || !_pathNormalizer.TryGetWorkspaceRelativePath(
                        workspaceRoot,
                        fullProjectPath,
                        out var normalizedProjectPath))
                {
                    return SolutionHierarchyResult.Failed(
                        $"Could not normalize project path '{project.FilePath}' from workspace file '{loadedPath}'.");
                }

                var folderPath = project.Parent is not null
                    ? NormalizeFolderPath(project.Parent.Path)
                    : null;

                var fullProjectPathKey = _pathComparison.CreateKey(fullProjectPath);
                if (!observedProjectPaths.Add(fullProjectPathKey)
                    || !projectFolderPaths.TryAdd(normalizedProjectPath, folderPath))
                {
                    return SolutionHierarchyResult.Failed(
                        $"Workspace file '{loadedPath}' contains duplicate project path '{normalizedProjectPath}'.");
                }
            }

            return SolutionHierarchyResult.Succeeded(folders, projectFolderPaths);
        }
        catch (Exception exception) when (exception is Microsoft.VisualStudio.SolutionPersistence.Model.SolutionException or System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return SolutionHierarchyResult.Failed(
                $"Could not load solution hierarchy for '{loadedPath}': {exception.Message}");
        }
    }

    private Dictionary<string, string> GetGlobalProperties(Guid workspaceId)
    {
        return _msBuildPropertiesProvider.Get(workspaceId)?.ToGlobalProperties() ?? [];
    }

    private static ProjectTargetFrameworksResult EvaluateProjectTargetFrameworks(
        string? projectPath,
        Microsoft.Build.Evaluation.ProjectCollection projectCollection)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return ProjectTargetFrameworksResult.Succeeded();
        }

        if (!File.Exists(projectPath))
        {
            return ProjectTargetFrameworksResult.Failed(
                $"Could not evaluate target frameworks because project file '{projectPath}' does not exist.");
        }

        try
        {
            var project = projectCollection.LoadProject(projectPath);
            var multipleTargetFrameworks = project.GetPropertyValue("TargetFrameworks");
            if (!string.IsNullOrWhiteSpace(multipleTargetFrameworks))
            {
                var evaluatedMultipleTargetFrameworks = multipleTargetFrameworks
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();

                return ProjectTargetFrameworksResult.Succeeded(evaluatedMultipleTargetFrameworks);
            }

            var singleTargetFramework = project.GetPropertyValue("TargetFramework");
            var evaluatedSingleTargetFramework = string.IsNullOrWhiteSpace(singleTargetFramework)
                ? []
                : new[] { singleTargetFramework.Trim() };

            return ProjectTargetFrameworksResult.Succeeded(evaluatedSingleTargetFramework);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return ProjectTargetFrameworksResult.Failed(
                $"Could not evaluate target frameworks for '{projectPath}': {exception.Message}");
        }
    }

    private static SolutionFolderInfo CreateSolutionFolderInfo(SolutionFolderModel folder)
    {
        var folderPath = NormalizeFolderPath(folder.Path);
        return new SolutionFolderInfo
        {
            Name = GetFolderName(folderPath),
            Path = folderPath,
            ParentPath = GetParentFolderPath(folderPath),
        };
    }

    private static string NormalizeFolderPath(string path)
    {
        return path.Replace('\\', '/').Trim('/').Trim();
    }

    private static string GetFolderName(string folderPath)
    {
        var lastSeparatorIndex = folderPath.LastIndexOf('/');
        return lastSeparatorIndex < 0 ? folderPath : folderPath[(lastSeparatorIndex + 1)..];
    }

    private static string? GetParentFolderPath(string folderPath)
    {
        var lastSeparatorIndex = folderPath.LastIndexOf('/');
        return lastSeparatorIndex < 0 ? null : folderPath[..lastSeparatorIndex];
    }
}
