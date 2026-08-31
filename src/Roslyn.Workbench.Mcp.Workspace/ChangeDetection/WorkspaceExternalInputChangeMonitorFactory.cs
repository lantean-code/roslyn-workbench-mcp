namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Constructs monitors for evaluated input memberships outside the Workspace root.
/// </summary>
internal sealed class WorkspaceExternalInputChangeMonitorFactory : IWorkspaceExternalInputChangeMonitorFactory
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceExternalInputChangeMonitorFactory"/> class.
    /// </summary>
    /// <param name="fileSystem">The filesystem abstraction used to create watchers and enumerate memberships.</param>
    /// <param name="pathComparison">The platform-aware path comparer.</param>
    public WorkspaceExternalInputChangeMonitorFactory(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
    }

    /// <inheritdoc/>
    public IWorkspaceExternalInputChangeMonitor Create(IReadOnlyList<WorkspaceExternalInputMembership> memberships)
    {
        var monitor = new WorkspaceExternalInputChangeMonitor(
            _fileSystem,
            _pathComparison,
            memberships);

        return monitor;
    }
}
