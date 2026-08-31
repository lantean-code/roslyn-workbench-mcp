namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Creates load-time change monitors rooted at a trusted Workspace boundary.
/// </summary>
internal interface IWorkspaceInputChangeMonitorFactory
{
    /// <summary>
    /// Creates an unstarted change monitor for a Workspace root.
    /// </summary>
    /// <param name="workspaceRoot">The root directory to watch recursively.</param>
    /// <returns>An unstarted input-change monitor.</returns>
    IWorkspaceInputChangeMonitor Create(string workspaceRoot);
}
