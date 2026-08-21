namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceInputChangeMonitorFactory : IWorkspaceInputChangeMonitorFactory
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspaceExternalInputChangeMonitorFactory _externalMonitorFactory;
    private readonly IWorkspacePathComparison _workspacePathComparison;

    public WorkspaceInputChangeMonitorFactory(
        IFileSystem fileSystem,
        IWorkspacePathComparison workspacePathComparison,
        IWorkspaceExternalInputChangeMonitorFactory externalMonitorFactory)
    {
        _fileSystem = fileSystem;
        _workspacePathComparison = workspacePathComparison;
        _externalMonitorFactory = externalMonitorFactory;
    }

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
