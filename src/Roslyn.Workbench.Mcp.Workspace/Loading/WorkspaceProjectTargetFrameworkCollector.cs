namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Collects target-framework progress reported by MSBuild and maps it to loaded Roslyn projects.
/// </summary>
internal sealed class WorkspaceProjectTargetFrameworkCollector : IProgress<ProjectLoadProgress>
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly Lock _sync = new();
    private readonly Dictionary<FileSystemPathKey, HashSet<string>> _targetFrameworksByProjectPath = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceProjectTargetFrameworkCollector"/> class.
    /// </summary>
    /// <param name="pathComparison">The platform-aware path comparison service.</param>
    public WorkspaceProjectTargetFrameworkCollector(IWorkspacePathComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    /// <summary>
    /// Records a resolved project's target framework from MSBuild load progress.
    /// </summary>
    /// <param name="value">The reported project load progress.</param>
    public void Report(ProjectLoadProgress value)
    {
        if (value.Operation != ProjectLoadOperation.Resolve
            || string.IsNullOrWhiteSpace(value.FilePath)
            || string.IsNullOrWhiteSpace(value.TargetFramework))
        {
            return;
        }

        var projectPath = _pathComparison.CreateKey(value.FilePath);
        lock (_sync)
        {
            if (!_targetFrameworksByProjectPath.TryGetValue(projectPath, out var targetFrameworks))
            {
                targetFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _targetFrameworksByProjectPath.Add(projectPath, targetFrameworks);
            }

            targetFrameworks.Add(value.TargetFramework);
        }
    }

    /// <summary>
    /// Maps collected target frameworks to the corresponding projects in a loaded solution.
    /// </summary>
    /// <param name="solution">The loaded solution whose project identities should be mapped.</param>
    /// <returns>The project target-framework map.</returns>
    public WorkspaceProjectTargetFrameworkMap CreateMap(Solution solution)
    {
        var projectsByPath = GroupProjectsByPath(solution);
        var targetFrameworksByProjectId = new Dictionary<ProjectId, string>();

        lock (_sync)
        {
            foreach (var (projectPath, projects) in projectsByPath)
            {
                if (!_targetFrameworksByProjectPath.TryGetValue(projectPath, out var targetFrameworks))
                {
                    continue;
                }

                if (projects.Count == 1 && targetFrameworks.Count == 1)
                {
                    targetFrameworksByProjectId.Add(projects[0].Id, targetFrameworks.Single());
                    continue;
                }

                AddTargetSpecificProjects(projects, targetFrameworks, targetFrameworksByProjectId);
            }
        }

        return new WorkspaceProjectTargetFrameworkMap(targetFrameworksByProjectId);
    }

    private Dictionary<FileSystemPathKey, List<Project>> GroupProjectsByPath(Solution solution)
    {
        var projectsByPath = new Dictionary<FileSystemPathKey, List<Project>>();
        foreach (var project in solution.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.FilePath))
            {
                continue;
            }

            var projectPath = _pathComparison.CreateKey(project.FilePath);
            if (!projectsByPath.TryGetValue(projectPath, out var projects))
            {
                projects = [];
                projectsByPath.Add(projectPath, projects);
            }

            projects.Add(project);
        }

        return projectsByPath;
    }

    private static void AddTargetSpecificProjects(
        IReadOnlyList<Project> projects,
        IReadOnlySet<string> targetFrameworks,
        Dictionary<ProjectId, string> targetFrameworksByProjectId)
    {
        foreach (var project in projects)
        {
            if (TryGetTargetFramework(project.Name, targetFrameworks, out var targetFramework))
            {
                targetFrameworksByProjectId.Add(project.Id, targetFramework);
            }
        }
    }

    private static bool TryGetTargetFramework(
        string projectName,
        IReadOnlySet<string> targetFrameworks,
        out string targetFramework)
    {
        targetFramework = string.Empty;
        foreach (var candidate in targetFrameworks)
        {
            if (HasTargetFrameworkDiscriminator(projectName, candidate))
            {
                targetFramework = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool HasTargetFrameworkDiscriminator(string projectName, string targetFramework)
    {
        return projectName.EndsWith($"({targetFramework})", StringComparison.OrdinalIgnoreCase);
    }
}
