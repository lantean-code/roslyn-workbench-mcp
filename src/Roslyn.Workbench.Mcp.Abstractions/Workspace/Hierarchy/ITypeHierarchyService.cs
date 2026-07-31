namespace Roslyn.Workbench.Mcp.Workspace.Hierarchy;

/// <summary>
/// Discovers source types related through class and interface inheritance.
/// </summary>
public interface ITypeHierarchyService
{
    /// <summary>
    /// Finds derived classes, derived interfaces and interface implementations within the selected projects.
    /// </summary>
    /// <param name="root">The class or interface at the root of the search.</param>
    /// <param name="solution">The immutable solution snapshot to search.</param>
    /// <param name="projects">The projects that define the search scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The discovered types and their shortest inheritance distance from <paramref name="root"/>.</returns>
    ValueTask<IReadOnlyList<TypeHierarchyMatch>> FindDerivedTypesAsync(
        INamedTypeSymbol root,
        Solution solution,
        IReadOnlyCollection<Project> projects,
        CancellationToken cancellationToken);
}
