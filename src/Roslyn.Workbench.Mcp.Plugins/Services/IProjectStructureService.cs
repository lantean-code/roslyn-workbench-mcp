
namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Loads project and solution structure metadata used by inspection tools.
/// </summary>
public interface IProjectStructureService
{
    /// <summary>
    /// Gets the target frameworks declared by the supplied project.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <returns>The target-framework evaluation result.</returns>
    ProjectTargetFrameworksResult GetTargetFrameworks(Project project);

    /// <summary>
    /// Gets the target frameworks declared by the supplied project path.
    /// </summary>
    /// <param name="projectPath">The project path.</param>
    /// <returns>The target-framework evaluation result.</returns>
    ProjectTargetFrameworksResult GetTargetFrameworks(string? projectPath);

    /// <summary>
    /// Loads solution-folder hierarchy and project membership information for the supplied solution path.
    /// </summary>
    /// <param name="loadedPath">The loaded solution path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The solution hierarchy-loading result.</returns>
    Task<SolutionHierarchyResult> GetSolutionHierarchyAsync(
        string? loadedPath,
        CancellationToken cancellationToken);
}
