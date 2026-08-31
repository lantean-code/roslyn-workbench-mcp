namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Applies the permitted Workspace lifecycle transitions and their session-level consequences.
/// </summary>
internal interface IWorkspaceStateTransitions
{
    /// <summary>
    /// Fires a lifecycle trigger from the supplied state.
    /// </summary>
    /// <param name="state">The current lifecycle state.</param>
    /// <param name="trigger">The requested transition.</param>
    /// <returns>The resulting lifecycle state.</returns>
    WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger);

    /// <summary>
    /// Marks a session stale after a monitored external input change.
    /// </summary>
    /// <param name="session">The session whose inputs changed.</param>
    /// <returns>The updated immutable session snapshot.</returns>
    WorkspaceSessionSnapshot ApplyExternalChangeDetected(WorkspaceSessionSnapshot session);
}
