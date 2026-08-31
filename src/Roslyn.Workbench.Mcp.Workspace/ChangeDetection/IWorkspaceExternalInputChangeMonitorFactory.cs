namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Creates monitors for evaluated file memberships located outside the Workspace root.
/// </summary>
internal interface IWorkspaceExternalInputChangeMonitorFactory
{
    /// <summary>
    /// Creates a monitor for the supplied external roots and their evaluated memberships.
    /// </summary>
    /// <param name="memberships">The external roots and matching rules to watch.</param>
    /// <returns>An unstarted external-input monitor.</returns>
    IWorkspaceExternalInputChangeMonitor Create(IReadOnlyList<WorkspaceExternalInputMembership> memberships);
}
