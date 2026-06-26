namespace Roslyn.Workbench.Mcp.Workspace;

/// <summary>
/// Creates workspace coordinators for host composition and tests.
/// </summary>
public static class WorkspaceCoordinatorFactory
{
    /// <summary>
    /// Creates a new workspace coordinator.
    /// </summary>
    /// <param name="options">The coordinator options.</param>
    /// <returns>The created workspace coordinator.</returns>
    public static IWorkspaceCoordinator Create(WorkspaceCoordinatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new WorkspaceCoordinator(options);
    }
}
