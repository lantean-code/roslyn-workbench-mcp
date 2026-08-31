namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Constructs root-input monitors with filesystem, path and external-membership dependencies.
/// </summary>
internal sealed class WorkspaceInputChangeMonitorFactory : IWorkspaceInputChangeMonitorFactory
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceExternalInputChangeMonitorFactory _externalMonitorFactory;
    private readonly IWorkspacePathComparison _workspacePathComparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceInputChangeMonitorFactory"/> class.
    /// </summary>
    /// <param name="fileSystem">The filesystem abstraction used to create watchers and inspect paths.</param>
    /// <param name="workspacePathComparison">The platform-aware path comparer.</param>
    /// <param name="externalMonitorFactory">The factory for evaluated inputs outside the Workspace root.</param>
    public WorkspaceInputChangeMonitorFactory(
        IFileSystem fileSystem,
        IWorkspacePathComparison workspacePathComparison,
        IWorkspaceExternalInputChangeMonitorFactory externalMonitorFactory)
    {
        _fileSystem = fileSystem;
        _workspacePathComparison = workspacePathComparison;
        _externalMonitorFactory = externalMonitorFactory;
    }

    /// <inheritdoc/>
    public IWorkspaceInputChangeMonitor Create(string workspaceRoot)
    {
        var monitor = new WorkspaceInputChangeMonitor(
            _fileSystem,
            _workspacePathComparison,
            _externalMonitorFactory,
            workspaceRoot);

        return monitor;
    }
}
