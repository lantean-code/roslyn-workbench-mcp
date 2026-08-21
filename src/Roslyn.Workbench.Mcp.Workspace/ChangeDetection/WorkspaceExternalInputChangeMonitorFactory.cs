namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceExternalInputChangeMonitorFactory : IWorkspaceExternalInputChangeMonitorFactory
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;

    public WorkspaceExternalInputChangeMonitorFactory(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
    }

    public IWorkspaceExternalInputChangeMonitor Create(IReadOnlyList<WorkspaceExternalInputMembership> memberships)
    {
        var monitor = new WorkspaceExternalInputChangeMonitor(
            _fileSystem,
            _pathComparison,
            memberships);

        return monitor;
    }
}
