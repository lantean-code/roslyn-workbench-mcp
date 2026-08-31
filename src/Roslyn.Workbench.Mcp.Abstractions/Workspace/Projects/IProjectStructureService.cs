using Roslyn.Workbench.Mcp.Workspace.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Projects;

/// <summary>
/// Loads project and solution structure metadata used by inspection tools.
/// </summary>
public interface IProjectStructureService
{
    /// <summary>
    /// Gets the target frameworks declared by the supplied project.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns the project.</param>
    /// <param name="project">The Roslyn project whose target frameworks are required.</param>
    /// <returns>The target-framework evaluation result.</returns>
    ProjectTargetFrameworksResult GetTargetFrameworks(Guid workspaceId, Project project);

    /// <summary>
    /// Gets the target frameworks declared by the supplied project path.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns the project.</param>
    /// <param name="projectPath">The project file path to evaluate, or <see langword="null"/> when unavailable.</param>
    /// <returns>The target-framework evaluation result.</returns>
    ProjectTargetFrameworksResult GetTargetFrameworks(Guid workspaceId, string? projectPath);

    /// <summary>
    /// Gets the target frameworks declared by the supplied projects using one request-scoped evaluation batch.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns the projects.</param>
    /// <param name="projects">The projects, in result order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The target-framework evaluation results in the same order as <paramref name="projects"/>.</returns>
    IReadOnlyList<ProjectTargetFrameworksResult> GetTargetFrameworks(
        Guid workspaceId,
        IReadOnlyList<Project> projects,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads solution-folder hierarchy and project membership information for the supplied workspace.
    /// </summary>
    /// <param name="workspace">The loaded workspace identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The solution hierarchy-loading result.</returns>
    Task<SolutionHierarchyResult> GetSolutionHierarchyAsync(
        WorkspaceIdentity workspace,
        CancellationToken cancellationToken);
}
