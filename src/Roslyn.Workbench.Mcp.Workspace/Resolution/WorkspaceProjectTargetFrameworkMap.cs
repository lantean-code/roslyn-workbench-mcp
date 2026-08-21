namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class WorkspaceProjectTargetFrameworkMap
{
    public static WorkspaceProjectTargetFrameworkMap Empty { get; } = CreateEmpty();

    private readonly Dictionary<ProjectId, string> _targetFrameworksByProjectId;

    public WorkspaceProjectTargetFrameworkMap(IReadOnlyDictionary<ProjectId, string> targetFrameworksByProjectId)
    {
        _targetFrameworksByProjectId = new Dictionary<ProjectId, string>(targetFrameworksByProjectId);
    }

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
