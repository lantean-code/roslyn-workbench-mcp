namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Matches Roslyn projects to the target-framework identities available to selectors.
/// </summary>
internal sealed class WorkspaceProjectTargetFrameworkMap
{
    /// <summary>
    /// Gets a map that contains no project target-framework identities.
    /// </summary>
    public static WorkspaceProjectTargetFrameworkMap Empty { get; } = CreateEmpty();

    private readonly Dictionary<ProjectId, string> _targetFrameworksByProjectId;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceProjectTargetFrameworkMap"/> class.
    /// </summary>
    /// <param name="targetFrameworksByProjectId">The target frameworks by project identifier.</param>
    public WorkspaceProjectTargetFrameworkMap(IReadOnlyDictionary<ProjectId, string> targetFrameworksByProjectId)
    {
        _targetFrameworksByProjectId = new Dictionary<ProjectId, string>(targetFrameworksByProjectId);
    }

    /// <summary>
    /// Determines whether a project is associated with a target framework.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="targetFramework">The target framework to compare with this project entry.</param>
    /// <returns><see langword="true"/> when the project has the target framework; otherwise, <see langword="false"/>.</returns>
    public bool Matches(ProjectId projectId, string targetFramework)
    {
        return _targetFrameworksByProjectId.TryGetValue(projectId, out var projectTargetFramework)
            && string.Equals(projectTargetFramework, targetFramework, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceProjectTargetFrameworkMap CreateEmpty()
    {
        var targetFrameworksByProjectId = new Dictionary<ProjectId, string>();
        return new WorkspaceProjectTargetFrameworkMap(targetFrameworksByProjectId);
    }
}
