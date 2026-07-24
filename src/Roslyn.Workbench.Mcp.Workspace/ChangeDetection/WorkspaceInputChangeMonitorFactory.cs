namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceInputChangeMonitorFactory : IWorkspaceInputChangeMonitorFactory
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _workspacePathComparison;

    public WorkspaceInputChangeMonitorFactory(
        IFileSystem fileSystem,
        IWorkspacePathComparison workspacePathComparison)
    {
        _fileSystem = fileSystem;
        _workspacePathComparison = workspacePathComparison;
    }

    public IWorkspaceInputChangeMonitor Create(string workspaceRoot)
    {
        return new WorkspaceInputChangeMonitor(
            _fileSystem,
            _workspacePathComparison,
            workspaceRoot);
    }
}
