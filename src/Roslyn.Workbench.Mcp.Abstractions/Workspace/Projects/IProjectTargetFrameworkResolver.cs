namespace Roslyn.Workbench.Mcp.Workspace.Projects;

/// <summary>
/// Resolves snapshot-scoped target-framework metadata for projects.
/// </summary>
public interface IProjectTargetFrameworkResolver
{
    /// <summary>
    /// Resolves target frameworks for one project.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns the project snapshot.</param>
    /// <param name="project">The project.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The target-framework evaluation result.</returns>
    ProjectTargetFrameworksResult Resolve(
        string workspaceId,
        Project project,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves target frameworks for projects in result order.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns the project snapshots.</param>
    /// <param name="projects">The projects, in result order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The target-framework evaluation results in the same order as <paramref name="projects"/>.</returns>
    IReadOnlyList<ProjectTargetFrameworksResult> Resolve(
        string workspaceId,
        IReadOnlyList<Project> projects,
        CancellationToken cancellationToken);
}
